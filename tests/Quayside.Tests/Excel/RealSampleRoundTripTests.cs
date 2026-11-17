using Quayside.Domain.Entities;
using Quayside.Excel;

namespace Quayside.Tests.Excel;

/// <summary>
/// Definition-of-done check against the original sample workbooks. They are NOT part of the repository;
/// point QUAYSIDE_SAMPLES at a local folder containing them to run this test.
/// </summary>
public class RealSampleRoundTripTests
{
    public const string Env = "QUAYSIDE_SAMPLES";

    private static readonly (string File, TemplateKind Kind)[] Order =
    {
        ("DefineCNTR.xlsx", TemplateKind.ContainerMaster),
        ("RTS.xlsx", TemplateKind.ReleaseToShipper),
        ("FRTN.xlsx", TemplateKind.FullReturn),
        ("F.RTN + Add to BL.xlsx", TemplateKind.FullReturnAttach),
        ("Read CNTRs.xlsx", TemplateKind.BlContainers),
        ("MT RTN.xlsx", TemplateKind.EmptyReturn),
    };

    [SkippableFact]
    public void Sample_workbooks_import_and_export_back_without_data_loss()
    {
        var dir = Environment.GetEnvironmentVariable(Env);
        Skip.If(string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir), $"{Env} not set");

        using var db = TestDatabase.CreateSqlite();
        using var ctx = db.NewContext();
        var bl = new BillOfLading { HouseNo = "SAMPLE-IMPORT", Direction = Direction.Import };
        ctx.BillsOfLading.Add(bl);
        ctx.SaveChanges();

        foreach (var (file, kind) in Order)
        {
            var bytes = File.ReadAllBytes(Path.Combine(dir!, file));
            Assert.Equal(kind, SheetIO.DetectKind(new MemoryStream(bytes)));

            var report = new TemplateImporter(ctx).Import(new MemoryStream(bytes), kind,
                new ImportOptions { Commit = true, Backfill = true, BillOfLadingId = bl.Id });
            Assert.True(report.Committed, $"{file}: " + string.Join("\n",
                report.FileErrors.Concat(report.Rows.Where(r => r.Status == RowStatus.Error)
                    .Select(r => $"row {r.RowNumber}: {string.Join("; ", r.Messages)}"))));
            Assert.Equal(0, report.ErrorRows);

            var original = SheetIO.Read(new MemoryStream(bytes), kind);
            var codes = original.Rows.Select(r => r.Text(Keys.Code)).OfType<string>().Distinct().ToList();
            var filter = kind == TemplateKind.BlContainers
                ? new ExportFilter(BillOfLadingId: bl.Id)
                : new ExportFilter(codes);
            using var exported = new TemplateExporter(ctx).Export(kind, filter);
            ExcelImportExportTests.AssertSameData(new MemoryStream(bytes), exported, kind);
        }
    }
}
