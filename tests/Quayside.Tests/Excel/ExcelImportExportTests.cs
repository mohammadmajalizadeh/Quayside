using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Quayside.Data;
using Quayside.Domain.Entities;
using Quayside.Excel;
using static Quayside.Tests.Excel.ExcelFixtures;

namespace Quayside.Tests.Excel;

public class ExcelImportExportTests
{
    private static readonly ImportOptions Commit = new() { Commit = true };
    private static readonly ImportOptions CommitBackfill = new() { Commit = true, Backfill = true };

    private static ImportReport Import(QuaysideDbContext ctx, Stream s, TemplateKind k, ImportOptions o) =>
        new TemplateImporter(ctx).Import(s, k, o);

    /// <summary>Re-reads both workbooks with the same reader and compares row values as multisets.</summary>
    internal static void AssertSameData(Stream original, XLWorkbook exported, TemplateKind kind)
    {
        original.Position = 0;
        var a = SheetIO.Read(original, kind);
        using var ms = new MemoryStream();
        exported.SaveAs(ms);
        ms.Position = 0;
        var b = SheetIO.Read(ms, kind);
        Assert.Empty(b.FileErrors);

        static string Key(SheetRow r) => string.Join("|", r.Values.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={Fmt(kv.Value)}"));
        static string Fmt(object? v) => v switch
        {
            null => "",
            decimal d => d.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("yyyy-MM-dd"),
            bool x => x ? "1" : "0",
            string s => s.Trim(),
            _ => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? "",
        };
        Assert.Equal(a.Rows.Select(Key).Order(), b.Rows.Select(Key).Order());
    }

    private static void AssertExactLayout(XLWorkbook wb, TemplateKind kind)
    {
        var spec = Templates.Get(kind);
        var ws = wb.Worksheet(1);
        Assert.Equal("Sheet1", ws.Name);
        for (var c = 0; c < spec.Columns.Count; c++)
            Assert.Equal(spec.Columns[c].Header, ws.Cell(1, c + 1).GetString());
        Assert.Equal(spec.Columns.Count, ws.Row(1).LastCellUsed()!.Address.ColumnNumber);
        Assert.Equal(new[] { "Sheet1" }.Concat(spec.ExtraEmptySheets), wb.Worksheets.Select(w => w.Name));
    }

    [Fact]
    public void Container_master_round_trip()
    {
        using var db = TestDatabase.CreateSqlite();
        using var ctx = db.NewContext();
        var r = Import(ctx, ContainerMaster(), TemplateKind.ContainerMaster, CommitBackfill);
        Assert.True(r.Committed, string.Join("\n", r.Rows.SelectMany(x => x.Messages)));
        Assert.Equal(4, ctx.Containers.Count());

        using var wb = new TemplateExporter(ctx).Export(TemplateKind.ContainerMaster, new ExportFilter(new[] { "501" }));
        AssertExactLayout(wb, TemplateKind.ContainerMaster);
        AssertSameData(ContainerMaster(), wb, TemplateKind.ContainerMaster);
        Assert.Equal(XLDataType.Number, wb.Worksheet(1).Cell(2, 1).Value.Type); // numeric codes stay numeric
    }

    [Fact]
    public void Export_cycle_round_trip_rts_frtn_and_attach()
    {
        using var db = TestDatabase.CreateSqlite();
        using var ctx = db.NewContext();
        Assert.True(Import(ctx, ContainerMaster(), TemplateKind.ContainerMaster, CommitBackfill).Committed);
        var rts = Import(ctx, Release(), TemplateKind.ReleaseToShipper, CommitBackfill);
        Assert.True(rts.Committed);
        var frtn = Import(ctx, FullReturn(), TemplateKind.FullReturn, Commit);
        Assert.True(frtn.Committed, string.Join("\n", frtn.Rows.SelectMany(x => x.Messages)));
        var attach = Import(ctx, FullReturnAttach(), TemplateKind.FullReturnAttach, CommitBackfill);
        Assert.True(attach.Committed, string.Join("\n", attach.Rows.SelectMany(x => x.Messages)));

        // QSDU100003 was released then returned full via the attach file: same cycle, now on B/L 880001
        var c3 = ctx.ContainerCycles.Include(c => c.BillOfLading).Single(c => c.Container!.Number == Num("QSDU100003"));
        Assert.Equal(new DateOnly(2026, 2, 3), c3.ReleasedToShipperOn);
        Assert.Equal(new DateOnly(2026, 2, 9), c3.FullReturnedOn);
        Assert.Equal("880001", c3.BillOfLading!.HouseNo);

        var exp = new TemplateExporter(ctx);
        foreach (var (kind, file, code) in new[]
                 {
                     (TemplateKind.ReleaseToShipper, Release(), "7001"),
                     (TemplateKind.FullReturn, FullReturn(), "7002"),
                     (TemplateKind.FullReturnAttach, FullReturnAttach(), "7003X"),
                 })
        {
            using var wb = exp.Export(kind, new ExportFilter(new[] { code }));
            AssertExactLayout(wb, kind);
            AssertSameData(file, wb, kind);
        }
    }

    [Fact]
    public void Import_cycle_round_trip_bl_containers_and_empty_return()
    {
        using var db = TestDatabase.CreateSqlite();
        using var ctx = db.NewContext();
        var bl = new BillOfLading { HouseNo = "IMP-1", Direction = Direction.Import, DeliveryOrderOn = new DateOnly(2026, 2, 25) };
        ctx.BillsOfLading.Add(bl);
        ctx.SaveChanges();

        var r = Import(ctx, BlContainers(), TemplateKind.BlContainers, new ImportOptions { Commit = true, BillOfLadingId = bl.Id });
        Assert.True(r.Committed, string.Join("\n", r.Rows.SelectMany(x => x.Messages)));
        foreach (var c in ctx.ContainerCycles) c.DischargedOn = new DateOnly(2026, 2, 20);
        ctx.SaveChanges();

        var mt = Import(ctx, EmptyReturn(), TemplateKind.EmptyReturn, new ImportOptions { Commit = true, Backfill = false });
        Assert.False(mt.Committed); // depot and body states are not defined yet
        Assert.Contains(mt.Rows.SelectMany(x => x.Messages), m => m.Contains("Depot"));

        ctx.Depots.Add(new Depot { Name = "North Yard" });
        ctx.BodyStates.AddRange(new BodyState { Code = 1, Name = "Sound" }, new BodyState { Code = 3, Name = "Damaged" });
        ctx.SaveChanges();
        mt = Import(ctx, EmptyReturn(), TemplateKind.EmptyReturn, Commit);
        Assert.True(mt.Committed, string.Join("\n", mt.Rows.SelectMany(x => x.Messages)));
        Assert.All(mt.Rows, x => Assert.Equal(RowStatus.Ok, x.Status)); // prerequisites met, no backfill needed

        var exp = new TemplateExporter(ctx);
        using (var wb = exp.Export(TemplateKind.BlContainers, new ExportFilter(BillOfLadingId: bl.Id)))
        {
            AssertExactLayout(wb, TemplateKind.BlContainers);
            AssertSameData(BlContainers(), wb, TemplateKind.BlContainers);
        }
        using (var wb = exp.Export(TemplateKind.EmptyReturn, new ExportFilter(new[] { "7004" })))
        {
            AssertExactLayout(wb, TemplateKind.EmptyReturn);
            Assert.Equal("CNTR ", wb.Worksheet(1).Cell(1, 2).GetString());
            AssertSameData(EmptyReturn(), wb, TemplateKind.EmptyReturn);
        }
    }

    [Fact]
    public void Full_return_attach_writes_text_dates_like_the_source()
    {
        using var db = TestDatabase.CreateSqlite();
        using var ctx = db.NewContext();
        Import(ctx, FullReturnAttach(), TemplateKind.FullReturnAttach, CommitBackfill);
        using var wb = new TemplateExporter(ctx).Export(TemplateKind.FullReturnAttach);
        Assert.Equal("2026/02/09", wb.Worksheet(1).Cell(2, 4).GetText());
    }

    [Fact]
    public void Preview_does_not_save()
    {
        using var db = TestDatabase.CreateSqlite();
        using var ctx = db.NewContext();
        var r = Import(ctx, ContainerMaster(), TemplateKind.ContainerMaster, new ImportOptions { Backfill = true });
        Assert.False(r.Committed);
        Assert.False(r.HasErrors);
        Assert.Equal(0, ctx.Containers.Count());
        Assert.Equal(0, ctx.Lines.Count());
    }

    [Fact]
    public void Missing_reference_without_backfill_blocks_whole_file_unless_valid_rows_only()
    {
        using var db = TestDatabase.CreateSqlite();
        using var ctx = db.NewContext();
        ctx.Lines.Add(new ShippingLine { Code = "QSL", Name = "QS Line" });
        ctx.SaveChanges();

        var r = Import(ctx, Build(new[] { "Code", "CNTR", "Size", "Type", "TW", "Line" },
                new object?[] { 1, Num("QSDU300001"), 20, "DC", 2200, "QSL" },
                new object?[] { 1, Num("QSDU300002"), 20, "DC", 2200, "NOPE" }),
            TemplateKind.ContainerMaster, Commit);
        Assert.False(r.Committed);
        Assert.Equal(1, r.ErrorRows);
        Assert.Equal(0, ctx.Containers.Count());

        r = Import(ctx, Build(new[] { "Code", "CNTR", "Size", "Type", "TW", "Line" },
                new object?[] { 1, Num("QSDU300001"), 20, "DC", 2200, "QSL" },
                new object?[] { 1, Num("QSDU300002"), 20, "DC", 2200, "NOPE" }),
            TemplateKind.ContainerMaster, new ImportOptions { Commit = true, ImportValidRowsOnly = true });
        Assert.True(r.Committed);
        Assert.Equal(Num("QSDU300001"), ctx.Containers.Single().Number);
        Assert.Equal(0, ctx.Lines.Count(l => l.Code == "NOPE"));
    }

    [Fact]
    public void BR_C5_duplicate_container_in_file_is_rejected()
    {
        using var db = TestDatabase.CreateSqlite();
        using var ctx = db.NewContext();
        var r = Import(ctx, Build(new[] { "Code", "CNTR", "Size", "Type" },
                new object?[] { 1, Num("QSDU300001"), 20, "DC" },
                new object?[] { 1, Dashed("QSDU300001"), 20, "DC" }),
            TemplateKind.ContainerMaster, Commit);
        Assert.Equal(RowStatus.Error, r.Rows[1].Status);
        Assert.Contains("BR-C5", r.Rows[1].Messages[0]);
    }

    [Fact]
    public void Release_twice_without_full_return_is_rejected()
    {
        using var db = TestDatabase.CreateSqlite();
        using var ctx = db.NewContext();
        Assert.True(Import(ctx, Release(), TemplateKind.ReleaseToShipper, CommitBackfill).Committed);
        var again = Import(ctx, Release(), TemplateKind.ReleaseToShipper, CommitBackfill);
        Assert.False(again.Committed);
        Assert.All(again.Rows, x => Assert.Equal(RowStatus.Error, x.Status));
    }

    [Fact]
    public void BR_L3_second_empty_return_with_different_date_is_rejected_same_date_is_idempotent()
    {
        using var db = TestDatabase.CreateSqlite();
        using var ctx = db.NewContext();
        Assert.True(Import(ctx, EmptyReturn(), TemplateKind.EmptyReturn, CommitBackfill).Committed);
        Assert.True(Import(ctx, EmptyReturn(), TemplateKind.EmptyReturn, Commit).Committed);

        var changed = Build(new[] { "Code", "CNTR ", "MT", "Body", "Depot" },
            new object?[] { 9, Num("QSDU200001"), new DateTime(2026, 3, 9), 1, "North Yard" });
        var r = Import(ctx, changed, TemplateKind.EmptyReturn, Commit);
        Assert.False(r.Committed);
        Assert.Contains("BR-L3", r.Rows[0].Messages[0]);
    }

    [Fact]
    public void BR_L2_empty_return_without_prerequisites_needs_backfill()
    {
        using var db = TestDatabase.CreateSqlite();
        using var ctx = db.NewContext();
        ctx.Containers.Add(new Container { Number = Num("QSDU200001"), SizeFeet = 20, TypeCode = "DC" });
        ctx.Containers.Add(new Container { Number = Num("QSDU200002"), SizeFeet = 40, TypeCode = "RF" });
        ctx.Depots.Add(new Depot { Name = "North Yard" });
        ctx.BodyStates.AddRange(new BodyState { Code = 1, Name = "a" }, new BodyState { Code = 3, Name = "b" });
        ctx.SaveChanges();
        foreach (var box in ctx.Containers.ToList())
            ctx.ContainerCycles.Add(new ContainerCycle { Container = box, Direction = Direction.Import });
        ctx.SaveChanges();

        var r = Import(ctx, EmptyReturn(), TemplateKind.EmptyReturn, Commit);
        Assert.All(r.Rows, x => Assert.Contains(x.Messages, m => m.Contains("BR-L2")));
        r = Import(ctx, EmptyReturn(), TemplateKind.EmptyReturn, CommitBackfill);
        Assert.True(r.Committed);
        Assert.All(r.Rows, x => Assert.Equal(RowStatus.Warning, x.Status));
    }

    [Fact]
    public void BR_C3_cargo_rules_apply_on_import()
    {
        using var db = TestDatabase.CreateSqlite();
        using var ctx = db.NewContext();
        var bl = new BillOfLading { HouseNo = "X", Direction = Direction.Import };
        ctx.BillsOfLading.Add(bl);
        ctx.SaveChanges();
        var file = Build(BlContainerHeaders,
            new object?[] { Num("QSDU400001"), 20, "DC", 2200, 1, "-", 0, "FCL", "3", "1203", null, 1, 1, 1, "ST", null, null });
        var r = Import(ctx, file, TemplateKind.BlContainers, new ImportOptions { Commit = true, BillOfLadingId = bl.Id });
        Assert.Contains("BR-C3", r.Rows[0].Messages[0]);
        Assert.Equal(0, ctx.Containers.Count()); // container registered by the failed row was rolled back
    }

    [Fact]
    public void Missing_required_column_is_a_file_error()
    {
        using var db = TestDatabase.CreateSqlite();
        using var ctx = db.NewContext();
        var r = Import(ctx, Build(new[] { "Code", "CNTR" }, new object?[] { 1, Num("QSDU300001") }),
            TemplateKind.FullReturn, Commit);
        Assert.Contains(r.FileErrors, e => e.Contains("FRTN"));
    }

    [Fact]
    public void Bad_cell_values_are_row_errors()
    {
        using var db = TestDatabase.CreateSqlite();
        using var ctx = db.NewContext();
        var r = Import(ctx, Build(new[] { "Code", "CNTR", "FRTN" }, new object?[] { 1, Num("QSDU300001"), "not a date" }),
            TemplateKind.FullReturn, CommitBackfill);
        Assert.Equal(RowStatus.Error, r.Rows[0].Status);
    }

    [Theory]
    [InlineData(TemplateKind.ContainerMaster)]
    [InlineData(TemplateKind.BlContainers)]
    [InlineData(TemplateKind.FullReturnAttach)]
    [InlineData(TemplateKind.ReleaseToShipper)]
    [InlineData(TemplateKind.FullReturn)]
    [InlineData(TemplateKind.EmptyReturn)]
    public void Template_is_detected_from_headers(TemplateKind kind)
    {
        var file = kind switch
        {
            TemplateKind.ContainerMaster => ContainerMaster(),
            TemplateKind.BlContainers => BlContainers(),
            TemplateKind.FullReturnAttach => FullReturnAttach(),
            TemplateKind.ReleaseToShipper => Release(),
            TemplateKind.FullReturn => FullReturn(),
            _ => EmptyReturn(),
        };
        Assert.Equal(kind, SheetIO.DetectKind(file));
    }
}
