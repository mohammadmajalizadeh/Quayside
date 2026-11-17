namespace Quayside.Domain.Entities;

/// <summary>Equipment classes that carry their own demurrage rate.</summary>
public enum EquipmentClass { Dry20 = 1, Dry40 = 2, HighCube40 = 3, Dry45 = 4, Reefer = 5 }

public enum DemurrageKind { ImportDemurrage = 1, ExportDetention = 2 }

public class DemurrageTariff : Entity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public DemurrageKind Kind { get; set; } = DemurrageKind.ImportDemurrage;
    public string CurrencyCode { get; set; } = "USD";
    public int FreeDays { get; set; }
    public bool IsActive { get; set; } = true;
    public List<DemurrageSlab> Slabs { get; set; } = new();
}

/// <summary>Day range with a daily rate per equipment class. ToDay null = open-ended.</summary>
public class DemurrageSlab
{
    public int Id { get; set; }
    public int TariffId { get; set; }
    public int FromDay { get; set; }
    public int? ToDay { get; set; }
    public List<DemurrageRate> Rates { get; set; } = new();
}

public class DemurrageRate
{
    public int Id { get; set; }
    public int SlabId { get; set; }
    public EquipmentClass EquipmentClass { get; set; }
    public decimal DailyRate { get; set; }
}

public enum InvoiceSide { Sell = 1, Buy = 2 }
public enum InvoiceKind { Local = 1, Demurrage = 2, Freight = 3, CreditNote = 4, DebitNote = 5 }
public enum InvoiceStatus { Draft = 0, Final = 1, Cancelled = 2 }

public class Invoice : Entity
{
    /// <summary>Negative while draft (BR-I1); final invoices are identified by TypeCode + Serial.</summary>
    public int DraftNumber { get; set; }
    public string TypeCode { get; set; } = "INV";
    public int? Serial { get; set; }
    public InvoiceSide Side { get; set; } = InvoiceSide.Sell;
    public InvoiceKind Kind { get; set; } = InvoiceKind.Local;
    public InvoiceStatus Status { get; set; }
    public int? BillOfLadingId { get; set; }
    public BillOfLading? BillOfLading { get; set; }
    public int? PartyId { get; set; }
    public Party? Party { get; set; }
    public DateOnly IssuedOn { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public int? DemurrageTariffId { get; set; }
    /// <summary>For credit/debit notes: the invoice being adjusted.</summary>
    public int? AdjustsInvoiceId { get; set; }
    public string? Remark { get; set; }
    public List<InvoiceLine> Lines { get; set; } = new();
}

public class InvoiceLine
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public string Description { get; set; } = "";
    public string? ServiceCode { get; set; }
    public int? ContainerCycleId { get; set; }
    public ContainerCycle? ContainerCycle { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal VatPercent { get; set; }
    public DateOnly? PeriodFrom { get; set; }
    public DateOnly? PeriodTo { get; set; }
    public int? FreeDays { get; set; }
    public int? ChargeableDays { get; set; }
}

public class Payment : Entity
{
    public int PartyId { get; set; }
    public Party? Party { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public DateOnly PaidOn { get; set; }
    public string? Reference { get; set; }
    public List<PaymentAllocation> Allocations { get; set; } = new();
}

public class PaymentAllocation
{
    public int Id { get; set; }
    public int PaymentId { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public decimal Amount { get; set; }
}

public class ExchangeRate
{
    public int Id { get; set; }
    public string CurrencyCode { get; set; } = "";
    public DateOnly EffectiveOn { get; set; }
    /// <summary>Units of base currency per one unit of <see cref="CurrencyCode"/>.</summary>
    public decimal Rate { get; set; }
}
