namespace Quayside.Domain.Entities;

/// <summary>Physical container (equipment master).</summary>
public class Container : Entity
{
    /// <summary>Normalised ISO 6346 number: 4 letters + 7 digits, upper case.</summary>
    public string Number { get; set; } = "";
    public int SizeFeet { get; set; }
    /// <summary>ISO type group, e.g. DC, HC, RF, OT, FR, TK.</summary>
    public string TypeCode { get; set; } = "";
    public int? TareKg { get; set; }
    public int? MaxGrossKg { get; set; }
    public int? LineId { get; set; }
    public ShippingLine? Line { get; set; }
    /// <summary>Opaque reference carried in the "Code" column of the container template (SPEC OQ-X1).</summary>
    public string? ExternalCode { get; set; }
}

/// <summary>Commercial booking (minimal job record; full CRM workflow is a later feature).</summary>
public class Booking : Entity
{
    public string BookingNo { get; set; } = "";
    public DateOnly? BookedOn { get; set; }
    public int? ClientId { get; set; }
    public Party? Client { get; set; }
    public int? LineId { get; set; }
    public ShippingLine? Line { get; set; }
}

public class BillOfLading : Entity
{
    public string HouseNo { get; set; } = "";
    public string? MasterNo { get; set; }
    public string? FileNo { get; set; }
    public Direction Direction { get; set; }
    public TransportMode Mode { get; set; } = TransportMode.SeaContainer;
    public int? LineId { get; set; }
    public ShippingLine? Line { get; set; }
    public int? VoyageId { get; set; }
    public Voyage? Voyage { get; set; }
    public int? BookingId { get; set; }
    public Booking? Booking { get; set; }
    public int? ShipperId { get; set; }
    public Party? Shipper { get; set; }
    public int? ConsigneeId { get; set; }
    public Party? Consignee { get; set; }
    public string? ShipperText { get; set; }
    public string? ConsigneeText { get; set; }
    public string? NotifyText { get; set; }
    public int? PolId { get; set; }
    public Port? Pol { get; set; }
    public int? PodId { get; set; }
    public Port? Pod { get; set; }
    public string? GoodsDescription { get; set; }
    public string? HsCode { get; set; }
    public DateOnly? IssuedOn { get; set; }
    /// <summary>Delivery-order date; prerequisite for empty return (BR-L2).</summary>
    public DateOnly? DeliveryOrderOn { get; set; }
    public int? DemurrageTariffId { get; set; }
    public DemurrageTariff? DemurrageTariff { get; set; }
    public List<ContainerCycle> Cycles { get; set; } = new();
}

/// <summary>One use of a container: an import cycle or an export cycle.</summary>
public class ContainerCycle : Entity
{
    public int ContainerId { get; set; }
    public Container? Container { get; set; }
    public Direction Direction { get; set; }
    public int? BillOfLadingId { get; set; }
    public BillOfLading? BillOfLading { get; set; }
    public int? BookingId { get; set; }
    public Booking? Booking { get; set; }

    // cargo
    public bool IsFull { get; set; }
    public bool IsShipperOwned { get; set; }
    public string? MovementType { get; set; }
    public string? SealNo { get; set; }
    public string? DgClass { get; set; }
    public string? UnNumber { get; set; }
    public decimal? ReeferTempC { get; set; }
    public int? Packages { get; set; }
    public decimal? GrossWeightKg { get; set; }
    public decimal? NetWeightKg { get; set; }
    public CargoType CargoType { get; set; }
    public string? ExportPermit { get; set; }
    public string? Remark { get; set; }

    // export lifecycle
    public DateOnly? ReleasedToShipperOn { get; set; }
    public DateOnly? FullReturnedOn { get; set; }

    // import lifecycle
    public DateOnly? DischargedOn { get; set; }
    public DateOnly? GateOutOn { get; set; }
    public DateOnly? EmptyReturnedOn { get; set; }
    public int? EmptyReturnDepotId { get; set; }
    public Depot? EmptyReturnDepot { get; set; }
    public int? EmptyReturnBodyStateId { get; set; }
    public BodyState? EmptyReturnBodyState { get; set; }
    public bool EmptyReturnConfirmed { get; set; }

    // batch references carried by the Excel templates (SPEC OQ-X1)
    public string? ReleaseBatchRef { get; set; }
    public string? FullReturnBatchRef { get; set; }
    public string? EmptyReturnBatchRef { get; set; }
    public string? AttachBatchRef { get; set; }
}
