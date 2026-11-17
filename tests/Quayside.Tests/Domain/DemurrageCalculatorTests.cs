using Quayside.Domain.Demurrage;
using Quayside.Domain.Entities;
using Quayside.Domain.Rules;
using Quayside.Domain.Settings;

namespace Quayside.Tests.Domain;

public class DemurrageCalculatorTests
{
    private static readonly DemurrageOptions Defaults = DemurrageOptions.From(new QuaysideSettings());

    private static DemurrageSlab Slab(int from, int? to, params (EquipmentClass c, decimal r)[] rates) => new()
    {
        FromDay = from,
        ToDay = to,
        Rates = rates.Select(x => new DemurrageRate { EquipmentClass = x.c, DailyRate = x.r }).ToList(),
    };

    /// <summary>5 free days; days 6-10 at 10/20/25/30/40; day 11+ at 20/40/50/60/80.</summary>
    private static DemurrageTariff Tariff(int freeDays = 5) => new()
    {
        Code = "T1",
        FreeDays = freeDays,
        CurrencyCode = "USD",
        Slabs =
        {
            Slab(freeDays + 1, 10, (EquipmentClass.Dry20, 10), (EquipmentClass.Dry40, 20), (EquipmentClass.HighCube40, 25),
                (EquipmentClass.Dry45, 30), (EquipmentClass.Reefer, 40)),
            Slab(11, null, (EquipmentClass.Dry20, 20), (EquipmentClass.Dry40, 40), (EquipmentClass.HighCube40, 50),
                (EquipmentClass.Dry45, 60), (EquipmentClass.Reefer, 80)),
        },
    };

    private static readonly DateOnly Mar1 = new(2026, 3, 1);

    [Fact]
    public void BR_D2_default_counts_both_ends_inclusive()
    {
        Assert.Equal(1, DemurrageCalculator.CountDays(Mar1, Mar1, Defaults));
        Assert.Equal(10, DemurrageCalculator.CountDays(Mar1, new DateOnly(2026, 3, 10), Defaults));
    }

    [Theory]
    [InlineData(true, true, 10)]
    [InlineData(false, true, 9)]
    [InlineData(true, false, 9)]
    [InlineData(false, false, 8)]
    public void BR_D2_day_counting_is_configurable(bool start, bool end, int expected) =>
        Assert.Equal(expected, DemurrageCalculator.CountDays(Mar1, new DateOnly(2026, 3, 10),
            Defaults with { CountStartDay = start, CountEndDay = end }));

    [Fact]
    public void BR_D2_end_before_start_is_rejected() =>
        Assert.Throws<BusinessRuleException>(() => DemurrageCalculator.CountDays(Mar1, Mar1.AddDays(-1), Defaults));

    [Fact]
    public void BR_D3_tiered_charge_for_20ft()
    {
        // 14 days: 5 free, days 6-10 (5 x 10), days 11-14 (4 x 20) = 130
        var r = DemurrageCalculator.Calculate(Tariff(), Mar1, new DateOnly(2026, 3, 14), EquipmentClass.Dry20, Defaults);
        Assert.Equal(14, r.TotalDays);
        Assert.Equal(5, r.FreeDays);
        Assert.Equal(9, r.ChargeableDays);
        Assert.Equal(130m, r.Total);
        Assert.Equal(new[] { (6, 10, 5), (11, 14, 4) }, r.Lines.Select(l => (l.FromDay, l.ToDay, l.Days)));
    }

    [Theory]
    [InlineData(EquipmentClass.Dry40, 260)]       // 5x20 + 4x40
    [InlineData(EquipmentClass.HighCube40, 325)]  // 5x25 + 4x50
    [InlineData(EquipmentClass.Dry45, 390)]       // 5x30 + 4x60
    [InlineData(EquipmentClass.Reefer, 520)]      // 5x40 + 4x80
    public void BR_D3_each_equipment_class_uses_its_own_rate(EquipmentClass cls, decimal expected) =>
        Assert.Equal(expected, DemurrageCalculator.Calculate(Tariff(), Mar1, new DateOnly(2026, 3, 14), cls, Defaults).Total);

    [Fact]
    public void BR_D3_within_free_days_costs_nothing()
    {
        var r = DemurrageCalculator.Calculate(Tariff(), Mar1, new DateOnly(2026, 3, 5), EquipmentClass.Dry20, Defaults);
        Assert.Equal(0m, r.Total);
        Assert.Empty(r.Lines);
    }

    [Fact]
    public void BR_D3_zero_free_days_charges_from_day_of_discharge()
    {
        var r = DemurrageCalculator.Calculate(Tariff(freeDays: 0), Mar1, Mar1, EquipmentClass.Dry20, Defaults);
        Assert.Equal(1, r.ChargeableDays);
        Assert.Equal(10m, r.Total);
    }

    [Fact]
    public void BR_D1_slabs_may_start_at_day_one_and_free_days_still_apply()
    {
        var t = new DemurrageTariff
        {
            FreeDays = 3,
            Slabs = { Slab(1, 5, (EquipmentClass.Dry20, 10)), Slab(6, null, (EquipmentClass.Dry20, 30)) },
        };
        // 7 days: days 1-3 free, 4-5 at 10, 6-7 at 30 = 80
        Assert.Equal(80m, DemurrageCalculator.Calculate(t, Mar1, new DateOnly(2026, 3, 7), EquipmentClass.Dry20, Defaults).Total);
    }

    [Fact]
    public void BR_D3_missing_rate_is_error_without_fallback_and_uses_fallback_when_configured()
    {
        var t = new DemurrageTariff { Slabs = { Slab(1, null, (EquipmentClass.Dry40, 20)) } };
        var end = new DateOnly(2026, 3, 2);
        Assert.Throws<BusinessRuleException>(() => DemurrageCalculator.Calculate(t, Mar1, end, EquipmentClass.HighCube40, Defaults));

        var o = Defaults with { RateFallbacks = DemurrageOptions.ParseFallbacks("HighCube40=Dry40") };
        var r = DemurrageCalculator.Calculate(t, Mar1, end, EquipmentClass.HighCube40, o);
        Assert.Equal(40m, r.Total);
        Assert.Equal(EquipmentClass.Dry40, r.Lines[0].RateClass);
    }

    [Fact]
    public void BR_D3_fallback_cycles_do_not_loop()
    {
        var t = new DemurrageTariff { Slabs = { Slab(1, null, (EquipmentClass.Dry20, 1)) } };
        var o = Defaults with { RateFallbacks = DemurrageOptions.ParseFallbacks("Dry40=Dry45,Dry45=Dry40") };
        Assert.Throws<BusinessRuleException>(() => DemurrageCalculator.Calculate(t, Mar1, Mar1, EquipmentClass.Dry40, o));
    }

    [Theory]
    [InlineData(20, "DC", EquipmentClass.Dry20)]
    [InlineData(40, "DC", EquipmentClass.Dry40)]
    [InlineData(40, "hc", EquipmentClass.HighCube40)]
    [InlineData(45, "HC", EquipmentClass.Dry45)]
    [InlineData(40, "RF", EquipmentClass.Reefer)]
    [InlineData(20, "RF", EquipmentClass.Reefer)]
    public void BR_D3_classification_from_size_and_type(int size, string type, EquipmentClass expected) =>
        Assert.Equal(expected, DemurrageCalculator.Classify(size, type, Defaults));

    [Fact]
    public void BR_D3_classification_lists_are_configurable()
    {
        var o = DemurrageOptions.From(new QuaysideSettings { DemurrageReeferTypeCodes = "", DemurrageHighCubeTypeCodes = "HC" });
        Assert.Equal(EquipmentClass.Dry40, DemurrageCalculator.Classify(40, "RF", o));
    }

    [Fact]
    public void BR_D1_tariff_structure_problems_are_reported()
    {
        Assert.Contains(DemurrageCalculator.ValidateTariff(new DemurrageTariff()), v => v.Message.Contains("at least one"));

        var gap = new DemurrageTariff { Slabs = { Slab(1, 5), Slab(7, null) } };
        Assert.Contains(DemurrageCalculator.ValidateTariff(gap), v => v.Message.Contains("contiguous"));

        var closed = new DemurrageTariff { Slabs = { Slab(1, 5) } };
        Assert.Contains(DemurrageCalculator.ValidateTariff(closed), v => v.Message.Contains("open-ended"));

        var lateStart = new DemurrageTariff { FreeDays = 2, Slabs = { Slab(5, null) } };
        Assert.Contains(DemurrageCalculator.ValidateTariff(lateStart), v => v.Message.Contains("first slab"));

        var negative = new DemurrageTariff { Slabs = { Slab(1, null, (EquipmentClass.Dry20, -1)) } };
        Assert.Contains(DemurrageCalculator.ValidateTariff(negative), v => v.Message.Contains("negative"));

        Assert.Empty(DemurrageCalculator.ValidateTariff(Tariff()));
    }

    [Fact]
    public void Invalid_fallback_text_is_rejected() =>
        Assert.Throws<FormatException>(() => DemurrageOptions.ParseFallbacks("Big=Small"));
}
