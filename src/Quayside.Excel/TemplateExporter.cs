using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Quayside.Data;
using Quayside.Domain.Entities;
using Quayside.Domain.Rules;

namespace Quayside.Excel;

/// <param name="BatchRefs">Only rows imported with these "Code" values (null = all).</param>
/// <param name="BillOfLadingId">Required for <see cref="TemplateKind.BlContainers"/>; optional filter otherwise.</param>
public sealed record ExportFilter(IReadOnlyCollection<string>? BatchRefs = null, int? BillOfLadingId = null);

/// <summary>Writes database contents back in the template layouts (lossless with <see cref="TemplateImporter"/>).</summary>
public sealed class TemplateExporter(QuaysideDbContext db)
{
    public XLWorkbook Export(TemplateKind kind, ExportFilter? filter = null) =>
        SheetIO.Write(kind, Rows(kind, filter ?? new ExportFilter()));

    public void Export(TemplateKind kind, ExportFilter? filter, Stream output)
    {
        using var wb = Export(kind, filter);
        wb.SaveAs(output);
    }

    public IReadOnlyList<Dictionary<string, object?>> Rows(TemplateKind kind, ExportFilter f)
    {
        bool InBatch(string? r) => f.BatchRefs is null || (r is not null && f.BatchRefs.Contains(r));

        IQueryable<ContainerCycle> cycles = db.ContainerCycles
            .Include(c => c.Container).ThenInclude(c => c!.Line)
            .Include(c => c.BillOfLading).Include(c => c.Booking)
            .Include(c => c.EmptyReturnDepot).Include(c => c.EmptyReturnBodyState)
            .OrderBy(c => c.Id);
        if (f.BillOfLadingId is { } blId) cycles = cycles.Where(c => c.BillOfLadingId == blId);

        switch (kind)
        {
            case TemplateKind.ContainerMaster:
                return db.Containers.Include(c => c.Line).OrderBy(c => c.Id).AsEnumerable()
                    .Where(c => InBatch(c.ExternalCode))
                    .Select(c => new Dictionary<string, object?>
                    {
                        [Keys.Code] = c.ExternalCode, [Keys.Container] = c.Number, [Keys.Size] = c.SizeFeet,
                        [Keys.Type] = c.TypeCode, [Keys.Tare] = c.TareKg, [Keys.Line] = c.Line?.Code,
                    }).ToList();

            case TemplateKind.BlContainers:
                if (f.BillOfLadingId is null) throw new ArgumentException("BillOfLadingId is required for this template.");
                return cycles.AsEnumerable().Select(c =>
                {
                    var d = Cargo(c);
                    d[Keys.Container] = c.Container!.Number;
                    d[Keys.Size] = c.Container.SizeFeet;
                    d[Keys.Type] = c.Container.TypeCode;
                    d[Keys.Tare] = c.Container.TareKg;
                    d[Keys.Remark] = c.Remark;
                    return d;
                }).ToList();

            case TemplateKind.FullReturnAttach:
                return cycles.Where(c => c.FullReturnedOn != null && c.BillOfLadingId != null).AsEnumerable()
                    .Where(c => InBatch(c.AttachBatchRef))
                    .Select(c =>
                    {
                        var d = Cargo(c);
                        d[Keys.Code] = c.AttachBatchRef;
                        d[Keys.HouseBl] = c.BillOfLading!.HouseNo;
                        d[Keys.Container] = c.Container!.Number;
                        d[Keys.Date] = c.FullReturnedOn;
                        return d;
                    }).ToList();

            case TemplateKind.ReleaseToShipper:
                return cycles.Where(c => c.ReleasedToShipperOn != null).AsEnumerable()
                    .Where(c => InBatch(c.ReleaseBatchRef))
                    .Select(c => new Dictionary<string, object?>
                    {
                        [Keys.Code] = c.ReleaseBatchRef, [Keys.Container] = c.Container!.Number,
                        [Keys.Date] = c.ReleasedToShipperOn, [Keys.Booking] = c.Booking?.BookingNo,
                    }).ToList();

            case TemplateKind.FullReturn:
                return cycles.Where(c => c.FullReturnedOn != null).AsEnumerable()
                    .Where(c => InBatch(c.FullReturnBatchRef))
                    .Select(c => new Dictionary<string, object?>
                    {
                        [Keys.Code] = c.FullReturnBatchRef, [Keys.Container] = c.Container!.Number,
                        [Keys.Date] = c.FullReturnedOn,
                    }).ToList();

            case TemplateKind.EmptyReturn:
                return cycles.Where(c => c.EmptyReturnedOn != null).AsEnumerable()
                    .Where(c => InBatch(c.EmptyReturnBatchRef))
                    .Select(c => new Dictionary<string, object?>
                    {
                        [Keys.Code] = c.EmptyReturnBatchRef, [Keys.Container] = c.Container!.Number,
                        [Keys.Date] = c.EmptyReturnedOn, [Keys.BodyState] = c.EmptyReturnBodyState?.Code,
                        [Keys.Depot] = c.EmptyReturnDepot?.Name,
                    }).ToList();

            default:
                throw new NotSupportedException(kind.ToString());
        }
    }

    private static Dictionary<string, object?> Cargo(ContainerCycle c) => new()
    {
        [Keys.Full] = c.IsFull,
        [Keys.Seal] = c.SealNo,
        [Keys.Soc] = c.IsShipperOwned,
        [Keys.Movement] = c.MovementType,
        [Keys.DgClass] = c.DgClass,
        [Keys.UnNumber] = c.UnNumber,
        [Keys.ReeferTemp] = c.ReeferTempC,
        [Keys.Packages] = c.Packages,
        [Keys.Gross] = c.GrossWeightKg,
        [Keys.Net] = c.NetWeightKg,
        [Keys.CargoType] = ContainerRules.ToCode(c.CargoType),
        [Keys.ExportPermit] = c.ExportPermit,
    };
}
