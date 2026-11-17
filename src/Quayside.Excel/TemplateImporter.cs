using Microsoft.EntityFrameworkCore;
using Quayside.Data;
using Quayside.Domain.Entities;
using Quayside.Domain.Rules;
using Quayside.Domain.Settings;

namespace Quayside.Excel;

public enum RowStatus { Ok, Warning, Error }

public sealed record RowResult(int RowNumber, string? Container, RowStatus Status, IReadOnlyList<string> Messages);

public sealed class ImportOptions
{
    /// <summary>Save changes. False = preview only.</summary>
    public bool Commit { get; init; }
    /// <summary>Save the valid rows even if other rows have errors. Default: all-or-nothing.</summary>
    public bool ImportValidRowsOnly { get; init; }
    /// <summary>
    /// Historical backfill: create missing lines, depots, body states, bookings, B/Ls, containers and cycles,
    /// and skip lifecycle prerequisite checks (BR-L2). Every such action is reported as a warning.
    /// </summary>
    public bool Backfill { get; init; }
    /// <summary>Target bill of lading for <see cref="TemplateKind.BlContainers"/>.</summary>
    public int? BillOfLadingId { get; init; }
}

public sealed class ImportReport
{
    public required TemplateKind Kind { get; init; }
    public List<string> FileErrors { get; } = new();
    public List<string> FileWarnings { get; } = new();
    public List<RowResult> Rows { get; } = new();
    public bool Committed { get; set; }
    public int ErrorRows => Rows.Count(r => r.Status == RowStatus.Error);
    public int ImportedRows => Rows.Count(r => r.Status != RowStatus.Error);
    public bool HasErrors => FileErrors.Count > 0 || ErrorRows > 0;
}

/// <summary>Imports template workbooks into the database applying the domain rules.</summary>
public sealed class TemplateImporter
{
    private readonly QuaysideDbContext _db;
    private readonly QuaysideSettings _settings;

    public TemplateImporter(QuaysideDbContext db, QuaysideSettings? settings = null)
    {
        _db = db;
        _settings = settings ?? SettingsStore.Load(db);
    }

    public ImportReport Import(Stream xlsx, TemplateKind kind, ImportOptions options)
    {
        var data = SheetIO.Read(xlsx, kind);
        return Import(data, options);
    }

    public ImportReport Import(SheetData data, ImportOptions options)
    {
        var report = new ImportReport { Kind = data.Template.Kind };
        report.FileErrors.AddRange(data.FileErrors);
        report.FileWarnings.AddRange(data.FileWarnings);

        BillOfLading? targetBl = null;
        if (data.Template.Kind == TemplateKind.BlContainers)
        {
            targetBl = options.BillOfLadingId is { } id ? _db.BillsOfLading.Include(b => b.Line).FirstOrDefault(b => b.Id == id) : null;
            if (targetBl is null) report.FileErrors.Add("Select the bill of lading to attach these containers to.");
        }
        if (report.FileErrors.Count > 0) return report;

        // BR-C5: duplicates within the file
        var dups = ContainerRules.FindDuplicates(data.Rows.Select(r => r.Text(Keys.Container) ?? "").Where(s => s.Length > 0)).ToHashSet();
        var seen = new HashSet<string>();

        using var tx = options.Commit ? _db.Database.BeginTransaction() : null;
        foreach (var row in data.Rows)
        {
            var ctx = new RowContext(row);
            ctx.Errors.AddRange(row.CellErrors);
            var number = row.Text(Keys.Container);
            if (number is not null && dups.Contains(number) && !seen.Add(number))
                ctx.Error("BR-C5", $"Container {number} appears more than once in this file.");

            if (ctx.Errors.Count == 0)
            {
                try
                {
                    switch (data.Template.Kind)
                    {
                        case TemplateKind.ContainerMaster: ImportMaster(ctx, options); break;
                        case TemplateKind.BlContainers: ImportBlContainer(ctx, targetBl!, options); break;
                        case TemplateKind.FullReturnAttach: ImportFullReturnAttach(ctx, options); break;
                        case TemplateKind.ReleaseToShipper: ImportRelease(ctx, options); break;
                        case TemplateKind.FullReturn: ImportFullReturn(ctx, options); break;
                        case TemplateKind.EmptyReturn: ImportEmptyReturn(ctx, options); break;
                    }
                }
                catch (BusinessRuleException ex)
                {
                    foreach (var v in ex.Violations) ctx.Add(v);
                }
                catch (FormatException ex)
                {
                    ctx.Errors.Add(ex.Message);
                }
            }
            if (ctx.Errors.Count > 0) ctx.Rollback(_db);
            report.Rows.Add(ctx.ToResult(number));
        }

        var save = options.Commit && (!report.HasErrors || options.ImportValidRowsOnly);
        if (save)
        {
            _db.SaveChanges();
            tx!.Commit();
            report.Committed = true;
        }
        else
        {
            _db.ChangeTracker.Clear();
        }
        return report;
    }

    // ---------- templates ----------

    private void ImportMaster(RowContext ctx, ImportOptions o)
    {
        var row = ctx.Row;
        var number = ValidNumber(ctx);
        var size = row.Int(Keys.Size)!.Value;
        var type = row.Text(Keys.Type)!.ToUpperInvariant();
        var line = ResolveLine(ctx, row.Text(Keys.Line), o);
        if (ctx.Errors.Count > 0) return;

        var box = FindContainer(number);
        if (box is null)
        {
            box = ctx.Track(_db.Containers.Add(new Container { Number = number }).Entity);
        }
        else if (box.SizeFeet != size || !string.Equals(box.TypeCode, type, StringComparison.OrdinalIgnoreCase))
        {
            ctx.Warn($"Updated size/type of {number} from {box.SizeFeet}' {box.TypeCode} to {size}' {type}.");
        }
        box.SizeFeet = size;
        box.TypeCode = type;
        box.TareKg = row.Int(Keys.Tare) ?? box.TareKg;
        box.Line = line ?? box.Line;
        box.ExternalCode = row.Text(Keys.Code);
    }

    private void ImportBlContainer(RowContext ctx, BillOfLading bl, ImportOptions o)
    {
        var row = ctx.Row;
        var number = ValidNumber(ctx);
        if (ctx.Errors.Count > 0) return;
        foreach (var v in ContainerRules.ValidateOwnerPrefix(number, bl.Line)) ctx.Add(v);

        var box = FindContainer(number);
        if (box is null)
        {
            if (row.Int(Keys.Size) is not { } size || string.IsNullOrWhiteSpace(row.Text(Keys.Type)))
            {
                ctx.Error("BR-C8", $"Container {number} is not registered; Size and Type are needed to register it.");
                return;
            }
            box = ctx.Track(_db.Containers.Add(new Container
            {
                Number = number, SizeFeet = size, TypeCode = row.Text(Keys.Type)!.ToUpperInvariant(),
                TareKg = row.Int(Keys.Tare), LineId = bl.LineId,
            }).Entity);
            ctx.Warn($"Registered new container {number}.");
        }
        else
        {
            foreach (var v in ContainerRules.ValidateAgainstMaster(box, row.Int(Keys.Size), row.Text(Keys.Type))) ctx.Add(v);
        }

        if (CyclesFor(box).Any(c => c.BillOfLadingId == bl.Id || c.BillOfLading == bl))
            ctx.Error("BR-C5", $"Container {number} is already on B/L {bl.HouseNo}.");
        if (ctx.Errors.Count > 0) return;

        var cycle = new ContainerCycle { Container = box, Direction = bl.Direction, BillOfLading = bl, Remark = row.Text(Keys.Remark) };
        ApplyCargo(ctx, cycle, box);
        if (ctx.Errors.Count > 0) return;
        ctx.Track(_db.ContainerCycles.Add(cycle).Entity);
    }

    private void ImportFullReturnAttach(RowContext ctx, ImportOptions o)
    {
        var row = ctx.Row;
        var number = ValidNumber(ctx);
        var houseNo = row.Text(Keys.HouseBl)!;
        if (ctx.Errors.Count > 0) return;

        var bl = _db.BillsOfLading.FindLocalFirst(b => b.HouseNo == houseNo && b.Direction == Direction.Export,
            b => b.HouseNo == houseNo && b.Direction == Direction.Export);
        if (bl is null)
        {
            if (!o.Backfill) { ctx.Error("W2", $"Export B/L {houseNo} not found."); return; }
            bl = ctx.Track(_db.BillsOfLading.Add(new BillOfLading { HouseNo = houseNo, Direction = Direction.Export }).Entity);
            ctx.Warn($"Backfill: created export B/L {houseNo}.");
        }
        if (bl.Line is null && bl.LineId is { } lid) bl.Line = _db.Lines.Find(lid);
        foreach (var v in ContainerRules.ValidateOwnerPrefix(number, bl.Line)) ctx.Add(v);

        var box = FindOrBackfillContainer(ctx, number, o);
        if (box is null || ctx.Errors.Count > 0) return;

        var cycle = OpenExportCycle(box, forFullReturn: true);
        if (cycle is not null && cycle.BillOfLadingId is { } other && other != bl.Id)
            ctx.Error("BR-C5", $"Container {number} is already attached to another B/L.");
        if (ctx.Errors.Count > 0) return;
        cycle ??= NewCycleOrError(ctx, box, Direction.Export, o);
        if (cycle is null) return;

        var date = row.Date(Keys.Date)!.Value;
        LifecycleRules.RecordFullReturn(cycle, date, _db.IsCoveredByDemurrageInvoice(cycle.Id));
        ApplyCargo(ctx, cycle, box);
        if (ctx.Errors.Count > 0) return;
        cycle.BillOfLading = bl;
        cycle.FullReturnBatchRef = row.Text(Keys.Code);
        cycle.AttachBatchRef = row.Text(Keys.Code);
    }

    private void ImportRelease(RowContext ctx, ImportOptions o)
    {
        var row = ctx.Row;
        var number = ValidNumber(ctx);
        var bookingNo = row.Text(Keys.Booking)!;
        if (ctx.Errors.Count > 0) return;

        var booking = _db.Bookings.FindLocalFirst(b => b.BookingNo == bookingNo, b => b.BookingNo == bookingNo);
        if (booking is null)
        {
            if (!o.Backfill) { ctx.Error("W2", $"Booking {bookingNo} not found."); return; }
            booking = ctx.Track(_db.Bookings.Add(new Booking { BookingNo = bookingNo }).Entity);
            ctx.Warn($"Backfill: created booking {bookingNo}.");
        }
        var box = FindOrBackfillContainer(ctx, number, o);
        if (box is null) return;

        var open = OpenExportCycle(box, forFullReturn: false);
        if (open is not null)
        {
            ctx.Error("W2", $"Container {number} is already released (since {open.ReleasedToShipperOn:yyyy-MM-dd}) and not yet returned full.");
            return;
        }
        var cycle = new ContainerCycle { Container = box, Direction = Direction.Export, Booking = booking };
        LifecycleRules.ReleaseToShipper(cycle, row.Date(Keys.Date)!.Value);
        cycle.ReleaseBatchRef = row.Text(Keys.Code);
        ctx.Track(_db.ContainerCycles.Add(cycle).Entity);
    }

    private void ImportFullReturn(RowContext ctx, ImportOptions o)
    {
        var number = ValidNumber(ctx);
        if (ctx.Errors.Count > 0) return;
        var box = FindOrBackfillContainer(ctx, number, o);
        if (box is null) return;
        var cycle = OpenExportCycle(box, forFullReturn: true) ?? NewCycleOrError(ctx, box, Direction.Export, o);
        if (cycle is null) return;
        LifecycleRules.RecordFullReturn(cycle, ctx.Row.Date(Keys.Date)!.Value, _db.IsCoveredByDemurrageInvoice(cycle.Id));
        cycle.FullReturnBatchRef = ctx.Row.Text(Keys.Code);
    }

    private void ImportEmptyReturn(RowContext ctx, ImportOptions o)
    {
        var row = ctx.Row;
        var number = ValidNumber(ctx);
        if (ctx.Errors.Count > 0) return;
        var box = FindOrBackfillContainer(ctx, number, o);
        if (box is null) return;

        Depot? depot = null;
        if (row.Text(Keys.Depot) is { } depotName)
        {
            depot = _db.Depots.FindLocalFirst(d => string.Equals(d.Name, depotName, StringComparison.OrdinalIgnoreCase),
                d => d.Name.ToUpper() == depotName.ToUpper());
            if (depot is null)
            {
                if (!o.Backfill) { ctx.Error("W3", $"Depot '{depotName}' not found."); return; }
                depot = ctx.Track(_db.Depots.Add(new Depot { Name = depotName }).Entity);
                ctx.Warn($"Backfill: created depot '{depotName}'.");
            }
        }
        BodyState? body = null;
        if (row.Int(Keys.BodyState) is { } code)
        {
            body = _db.BodyStates.FindLocalFirst(b => b.Code == code, b => b.Code == code);
            if (body is null)
            {
                if (!o.Backfill) { ctx.Error("W3", $"Body state {code} not defined."); return; }
                body = ctx.Track(_db.BodyStates.Add(new BodyState { Code = code, Name = $"Body state {code}" }).Entity);
                ctx.Warn($"Backfill: created body state {code}.");
            }
        }

        var date = row.Date(Keys.Date)!.Value;
        var cycles = CyclesFor(box).Where(c => c.Direction == Direction.Import).OrderByDescending(c => c.Id == 0 ? int.MaxValue : c.Id).ToList();
        var cycle = cycles.FirstOrDefault(c => c.EmptyReturnedOn is null)
                    ?? cycles.FirstOrDefault(c => c.EmptyReturnedOn == date);
        if (cycle is null && cycles.Count > 0)
        {
            ctx.Error("BR-L3", $"Container {number} already has an empty-return date ({cycles[0].EmptyReturnedOn:yyyy-MM-dd}).");
            return;
        }
        if (cycle?.EmptyReturnedOn == date)
        {
            ctx.Warn($"Empty return of {number} on {date:yyyy-MM-dd} is already recorded; row skipped.");
            return;
        }
        cycle ??= NewCycleOrError(ctx, box, Direction.Import, o);
        if (cycle is null) return;

        var bl = cycle.BillOfLading ?? (cycle.BillOfLadingId is { } bid ? _db.BillsOfLading.Find(bid) : null);
        var requirePrereq = _settings.EmptyReturnRequiresDischargeAndDo && !o.Backfill;
        if (o.Backfill && _settings.EmptyReturnRequiresDischargeAndDo && (cycle.DischargedOn is null || bl?.DeliveryOrderOn is null))
            ctx.Warn("Backfill: discharge/D-O prerequisite not checked (BR-L2).");
        LifecycleRules.RecordEmptyReturn(cycle, bl, date, depot, body, _db.IsCoveredByDemurrageInvoice(cycle.Id),
            allowOverwrite: false, requirePrerequisites: requirePrereq);
        cycle.EmptyReturnBatchRef = row.Text(Keys.Code);
    }

    // ---------- helpers ----------

    private string ValidNumber(RowContext ctx)
    {
        var issues = ContainerNumber.Validate(ctx.Row.Text(Keys.Container), out var n, _settings.ContainerCheckDigitIsError);
        foreach (var v in issues) ctx.Add(v);
        return n;
    }

    private Container? FindContainer(string number) =>
        _db.Containers.FindLocalFirst(c => c.Number == number, c => c.Number == number);

    private Container? FindOrBackfillContainer(RowContext ctx, string number, ImportOptions o)
    {
        var box = FindContainer(number);
        if (box is not null) return box;
        if (!o.Backfill)
        {
            ctx.Error("BR-C8", $"Container {number} is not registered. Import the container master first or use backfill.");
            return null;
        }
        ctx.Warn($"Backfill: registered container {number} with unknown size/type.");
        return ctx.Track(_db.Containers.Add(new Container { Number = number, TypeCode = "" }).Entity);
    }

    private ShippingLine? ResolveLine(RowContext ctx, string? code, ImportOptions o)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var line = _db.Lines.FindLocalFirst(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase),
            l => l.Code.ToUpper() == code.ToUpper());
        if (line is not null) return line;
        if (!o.Backfill) { ctx.Error("BR-C2", $"Line '{code}' is not defined."); return null; }
        ctx.Warn($"Backfill: created line '{code}'.");
        return ctx.Track(_db.Lines.Add(new ShippingLine { Code = code.ToUpperInvariant(), Name = code }).Entity);
    }

    private List<ContainerCycle> CyclesFor(Container box)
    {
        if (box.Id > 0) _db.ContainerCycles.Where(c => c.ContainerId == box.Id).Load();
        return _db.ContainerCycles.Local
            .Where(c => !c.IsDeleted && (c.Container == box || (box.Id > 0 && c.ContainerId == box.Id)))
            .ToList();
    }

    /// <summary>Latest export cycle that has not returned full. For a full return, a released cycle is preferred.</summary>
    private ContainerCycle? OpenExportCycle(Container box, bool forFullReturn) =>
        CyclesFor(box)
            .Where(c => c.Direction == Direction.Export && c.FullReturnedOn is null)
            .OrderByDescending(c => forFullReturn && c.ReleasedToShipperOn is not null)
            .ThenByDescending(c => c.Id == 0 ? int.MaxValue : c.Id)
            .FirstOrDefault();

    private ContainerCycle? NewCycleOrError(RowContext ctx, Container box, Direction direction, ImportOptions o)
    {
        if (!o.Backfill)
        {
            ctx.Error(direction == Direction.Export ? "W2" : "W3",
                $"No open {direction.ToString().ToLowerInvariant()} cycle for container {box.Number}.");
            return null;
        }
        ctx.Warn($"Backfill: created {direction.ToString().ToLowerInvariant()} cycle for {box.Number}.");
        return ctx.Track(_db.ContainerCycles.Add(new ContainerCycle { Container = box, Direction = direction }).Entity);
    }

    private void ApplyCargo(RowContext ctx, ContainerCycle cycle, Container box)
    {
        var r = ctx.Row;
        var probe = new ContainerCycle
        {
            DgClass = r.Text(Keys.DgClass), UnNumber = r.Text(Keys.UnNumber), GrossWeightKg = r.Decimal(Keys.Gross),
            CargoType = ContainerRules.ParseCargoType(r.Text(Keys.CargoType)),
        };
        foreach (var v in ContainerRules.ValidateCargoType(probe, box, _settings.OverweightFallbackKg)) ctx.Add(v);
        if (ctx.Errors.Count > 0) return;

        cycle.IsFull = r.Flag(Keys.Full) ?? cycle.IsFull;
        cycle.SealNo = r.Text(Keys.Seal);
        cycle.IsShipperOwned = r.Flag(Keys.Soc) ?? false;
        cycle.MovementType = r.Text(Keys.Movement);
        cycle.DgClass = probe.DgClass;
        cycle.UnNumber = probe.UnNumber;
        cycle.ReeferTempC = r.Decimal(Keys.ReeferTemp);
        cycle.Packages = r.Int(Keys.Packages);
        cycle.GrossWeightKg = probe.GrossWeightKg;
        cycle.NetWeightKg = r.Decimal(Keys.Net);
        cycle.CargoType = probe.CargoType;
        cycle.ExportPermit = r.Text(Keys.ExportPermit);
    }

    private sealed class RowContext(SheetRow row)
    {
        public SheetRow Row { get; } = row;
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
        private readonly List<object> _added = new();

        public T Track<T>(T entity) where T : class { _added.Add(entity); return entity; }
        public void Error(string rule, string msg) => Errors.Add($"[{rule}] {msg}");
        public void Warn(string msg) => Warnings.Add(msg);
        public void Add(RuleViolation v)
        {
            if (v.Severity == Severity.Error) Errors.Add($"[{v.Rule}] {v.Message}");
            else Warnings.Add($"[{v.Rule}] {v.Message}");
        }

        /// <summary>Detaches entities added by a failed row (rules validate before mutating existing ones).</summary>
        public void Rollback(DbContext db)
        {
            foreach (var e in _added) db.Entry(e).State = EntityState.Detached;
        }

        public RowResult ToResult(string? container) => new(Row.RowNumber, container,
            Errors.Count > 0 ? RowStatus.Error : Warnings.Count > 0 ? RowStatus.Warning : RowStatus.Ok,
            Errors.Concat(Warnings).ToList());
    }
}
