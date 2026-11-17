using Quayside.Domain.Entities;

namespace Quayside.Domain.Rules;

/// <summary>
/// Container lifecycle events (SPEC W2/W3, BR-L1..BR-L6). Every method validates first and
/// changes nothing when it throws <see cref="BusinessRuleException"/>.
/// </summary>
public static class LifecycleRules
{
    /// <summary>RTS: empty box released to the shipper; starts an export cycle.</summary>
    public static void ReleaseToShipper(ContainerCycle cycle, DateOnly on, bool coveredByDemurrageInvoice = false)
    {
        Require(cycle.Direction == Direction.Export, "W2", "Release to shipper applies to export cycles only.");
        if (cycle.ReleasedToShipperOn is not null && cycle.ReleasedToShipperOn != on)
            Require(!coveredByDemurrageInvoice, "BR-L4", "Release date cannot change while a detention invoice covers this container.");
        if (cycle.FullReturnedOn is { } fr)
            Require(on <= fr, "BR-L1", $"Release date {on:yyyy-MM-dd} is after the full-return date {fr:yyyy-MM-dd}.");
        cycle.ReleasedToShipperOn = on;
    }

    /// <summary>F.RTN: exporter returns the stuffed box.</summary>
    public static void RecordFullReturn(ContainerCycle cycle, DateOnly on, bool coveredByDemurrageInvoice = false)
    {
        Require(cycle.Direction == Direction.Export, "W2", "Full return applies to export cycles only.");
        if (cycle.FullReturnedOn is not null && cycle.FullReturnedOn != on)
            Require(!coveredByDemurrageInvoice, "BR-L4", "Full-return date cannot change while a detention invoice covers this container.");
        if (cycle.ReleasedToShipperOn is { } rts)
            Require(on >= rts, "BR-L1", $"Full-return date {on:yyyy-MM-dd} is before the release date {rts:yyyy-MM-dd}.");
        cycle.FullReturnedOn = on;
    }

    /// <summary>MT.RTN: importer returns the empty box to a depot.</summary>
    /// <param name="requirePrerequisites">BR-L2 (discharge date and D/O date must exist). Configurable.</param>
    /// <param name="allowOverwrite">BR-L3: an explicit "change" action may overwrite an existing return.</param>
    public static void RecordEmptyReturn(ContainerCycle cycle, BillOfLading? bl, DateOnly on, Depot? depot,
        BodyState? bodyState, bool coveredByDemurrageInvoice = false, bool allowOverwrite = false,
        bool requirePrerequisites = true)
    {
        var v = new List<RuleViolation>();
        if (cycle.Direction != Direction.Import)
            v.Add(new("W3", "Empty return applies to import cycles only."));
        if (requirePrerequisites)
        {
            if (cycle.DischargedOn is null)
                v.Add(new("BR-L2", "Empty return needs a discharge date first."));
            if (bl?.DeliveryOrderOn is null)
                v.Add(new("BR-L2", "Empty return needs the bill of lading's delivery-order date first."));
        }
        if (cycle.EmptyReturnedOn is not null && cycle.EmptyReturnedOn != on)
        {
            if (!allowOverwrite)
                v.Add(new("BR-L3", $"Container already has an empty-return date ({cycle.EmptyReturnedOn:yyyy-MM-dd})."));
            if (coveredByDemurrageInvoice)
                v.Add(new("BR-L4", "Empty-return date cannot change while a demurrage invoice covers this container."));
        }
        if (cycle.DischargedOn is { } d && on < d)
            v.Add(new("BR-L1", $"Empty-return date {on:yyyy-MM-dd} is before the discharge date {d:yyyy-MM-dd}."));
        if (v.Count > 0) throw new BusinessRuleException(v);

        cycle.EmptyReturnedOn = on;
        cycle.EmptyReturnDepot = depot;
        cycle.EmptyReturnDepotId = depot?.Id;
        cycle.EmptyReturnBodyState = bodyState;
        cycle.EmptyReturnBodyStateId = bodyState?.Id;
    }

    public static void ClearFullReturn(ContainerCycle cycle, bool coveredByDemurrageInvoice)
    {
        Require(cycle.FullReturnedOn is not null, "BR-L5", "There is no full-return date to remove.");
        Require(!coveredByDemurrageInvoice, "BR-L4", "Full-return date cannot be removed while a detention invoice covers this container.");
        cycle.FullReturnedOn = null;
    }

    public static void ClearEmptyReturn(ContainerCycle cycle, bool coveredByDemurrageInvoice)
    {
        Require(cycle.EmptyReturnedOn is not null, "BR-L5", "There is no empty-return date to remove.");
        Require(!coveredByDemurrageInvoice, "BR-L4", "Empty-return date cannot be removed while a demurrage invoice covers this container.");
        cycle.EmptyReturnedOn = null;
        cycle.EmptyReturnDepotId = null;
        cycle.EmptyReturnDepot = null;
        cycle.EmptyReturnBodyStateId = null;
        cycle.EmptyReturnBodyState = null;
        cycle.EmptyReturnConfirmed = false;
    }

    /// <summary>BR-L6: first voyage is fixed once containers are attached.</summary>
    public static void ChangeVoyage(BillOfLading bl, int? voyageId, int liveCycleCount)
    {
        if (bl.VoyageId == voyageId) return;
        Require(liveCycleCount == 0, "BR-L6", "The voyage cannot change while the bill of lading has containers.");
        bl.VoyageId = voyageId;
    }

    /// <summary>BR-L6: cargo units must be removed before the bill of lading is deleted.</summary>
    public static void EnsureCanDelete(BillOfLading bl, int liveCycleCount) =>
        Require(liveCycleCount == 0, "BR-L6", $"Remove the {liveCycleCount} container(s) from B/L {bl.HouseNo} first.");

    private static void Require(bool condition, string rule, string message)
    {
        if (!condition) throw new BusinessRuleException(rule, message);
    }
}
