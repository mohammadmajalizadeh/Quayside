namespace Quayside.Domain.Entities;

/// <summary>Common audit and soft-delete fields (SPEC BR-G1, BR-G3).</summary>
public abstract class Entity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

[Flags]
public enum PartyRole
{
    None = 0,
    Client = 1,
    Shipper = 2,
    Consignee = 4,
    Notify = 8,
    Forwarder = 16,
    LineAgent = 32,
    Carrier = 64,
    Bank = 128,
    TransitAgent = 256,
    DepotOperator = 512,
}

public enum Direction { Import = 1, Export = 2 }

public enum TransportMode { SeaContainer = 1, SeaBulk = 2, Air = 3, Road = 4, Rail = 5 }

public enum CargoType { Standard = 0, Dangerous = 1, OutOfGauge = 2 }

public class Party : Entity
{
    public string Code { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? NameFa { get; set; }
    public PartyRole Roles { get; set; }
    public string? NationalId { get; set; }
    public string? EconomicCode { get; set; }
    public string? AddressEn { get; set; }
    public string? AddressFa { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Remark { get; set; }
}

public class ShippingLine : Entity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Comma-separated ISO 6346 owner prefixes permitted for this line (BR-C2). Empty = any.</summary>
    public string? OwnerPrefixes { get; set; }
    public int? AgencyPartyId { get; set; }
    public Party? AgencyParty { get; set; }

    public IReadOnlyList<string> PrefixList() =>
        (OwnerPrefixes ?? "")
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.ToUpperInvariant())
            .ToList();
}

public class Vessel : Entity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? EdiCode { get; set; }
}

public class Port : Entity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public class Depot : Entity
{
    public string Name { get; set; } = "";
    public string? Code { get; set; }
}

public class BodyState : Entity
{
    public int Code { get; set; }
    public string Name { get; set; } = "";
}

public class Voyage : Entity
{
    public int VesselId { get; set; }
    public Vessel? Vessel { get; set; }
    public int? PortId { get; set; }
    public Port? Port { get; set; }
    public string? ImportVoyageNo { get; set; }
    public string? ExportVoyageNo { get; set; }
    public DateOnly? Eta { get; set; }
    public DateOnly? ArrivedOn { get; set; }
    public DateOnly? SailedOn { get; set; }
    public bool PaymentLocked { get; set; }
}

public class AppSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
