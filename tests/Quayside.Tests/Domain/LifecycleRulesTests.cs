using Quayside.Data;
using Quayside.Domain.Entities;
using Quayside.Domain.Rules;
using Quayside.Domain.Settings;

namespace Quayside.Tests.Domain;

public class LifecycleRulesTests
{
    private static readonly DateOnly D1 = new(2026, 3, 1);
    private static readonly DateOnly D5 = new(2026, 3, 5);
    private static readonly DateOnly D9 = new(2026, 3, 9);

    private static ContainerCycle Export() => new() { Direction = Direction.Export };
    private static ContainerCycle Import(DateOnly? discharged = null) => new() { Direction = Direction.Import, DischargedOn = discharged };
    private static BillOfLading BlWithDo() => new() { HouseNo = "H1", DeliveryOrderOn = D1 };

    private static string Rule(Action a) => Assert.Throws<BusinessRuleException>(a).Violations[0].Rule;

    [Fact]
    public void Export_cycle_release_then_full_return()
    {
        var c = Export();
        LifecycleRules.ReleaseToShipper(c, D1);
        LifecycleRules.RecordFullReturn(c, D5);
        Assert.Equal(D1, c.ReleasedToShipperOn);
        Assert.Equal(D5, c.FullReturnedOn);
        Assert.True(c.IsFull);
    }

    [Fact]
    public void BR_L1_full_return_before_release_is_rejected()
    {
        var c = Export();
        LifecycleRules.ReleaseToShipper(c, D5);
        Assert.Equal("BR-L1", Rule(() => LifecycleRules.RecordFullReturn(c, D1)));
        Assert.Null(c.FullReturnedOn);
    }

    [Fact]
    public void BR_L1_empty_return_before_discharge_is_rejected() =>
        Assert.Equal("BR-L1", Rule(() => LifecycleRules.RecordEmptyReturn(Import(D5), BlWithDo(), D1, null, null)));

    [Fact]
    public void BR_L2_empty_return_requires_discharge_and_do_dates()
    {
        var ex = Assert.Throws<BusinessRuleException>(() =>
            LifecycleRules.RecordEmptyReturn(Import(), new BillOfLading(), D5, null, null));
        Assert.Equal(2, ex.Violations.Count(v => v.Rule == "BR-L2"));
    }

    [Fact]
    public void BR_L2_prerequisites_can_be_disabled_by_setting()
    {
        var c = Import();
        LifecycleRules.RecordEmptyReturn(c, null, D5, null, null, requirePrerequisites: false);
        Assert.Equal(D5, c.EmptyReturnedOn);
    }

    [Fact]
    public void BR_L3_existing_empty_return_is_not_overwritten_without_explicit_change()
    {
        var c = Import(D1);
        var depot = new Depot { Id = 7, Name = "North yard" };
        LifecycleRules.RecordEmptyReturn(c, BlWithDo(), D5, depot, null);
        Assert.Equal("BR-L3", Rule(() => LifecycleRules.RecordEmptyReturn(c, BlWithDo(), D9, null, null)));
        LifecycleRules.RecordEmptyReturn(c, BlWithDo(), D9, depot, null, allowOverwrite: true);
        Assert.Equal(D9, c.EmptyReturnedOn);
        Assert.Equal(7, c.EmptyReturnDepotId);
    }

    [Fact]
    public void BR_L4_dates_locked_once_demurrage_invoiced()
    {
        var e = Export();
        LifecycleRules.ReleaseToShipper(e, D1);
        LifecycleRules.RecordFullReturn(e, D5);
        Assert.Equal("BR-L4", Rule(() => LifecycleRules.RecordFullReturn(e, D9, coveredByDemurrageInvoice: true)));
        Assert.Equal("BR-L4", Rule(() => LifecycleRules.ClearFullReturn(e, coveredByDemurrageInvoice: true)));

        var i = Import(D1);
        LifecycleRules.RecordEmptyReturn(i, BlWithDo(), D5, null, null);
        Assert.Equal("BR-L4", Rule(() => LifecycleRules.ClearEmptyReturn(i, coveredByDemurrageInvoice: true)));
        Assert.Equal(D5, i.EmptyReturnedOn);
    }

    [Fact]
    public void BR_L5_clearing_an_empty_date_is_an_error()
    {
        Assert.Equal("BR-L5", Rule(() => LifecycleRules.ClearFullReturn(Export(), false)));
        Assert.Equal("BR-L5", Rule(() => LifecycleRules.ClearEmptyReturn(Import(), false)));
    }

    [Fact]
    public void BR_L6_voyage_fixed_and_delete_blocked_while_containers_attached()
    {
        var bl = new BillOfLading { HouseNo = "H1", VoyageId = 1 };
        Assert.Equal("BR-L6", Rule(() => LifecycleRules.ChangeVoyage(bl, 2, liveCycleCount: 3)));
        Assert.Equal("BR-L6", Rule(() => LifecycleRules.EnsureCanDelete(bl, 3)));
        LifecycleRules.ChangeVoyage(bl, 2, 0);
        Assert.Equal(2, bl.VoyageId);
    }

    [Fact]
    public void Direction_mismatch_is_rejected()
    {
        Assert.Throws<BusinessRuleException>(() => LifecycleRules.ReleaseToShipper(Import(), D1));
        Assert.Throws<BusinessRuleException>(() => LifecycleRules.RecordEmptyReturn(Export(), null, D1, null, null, requirePrerequisites: false));
    }
}

public class SettingsTests
{
    [Fact]
    public void Defaults_match_product_owner_decisions()
    {
        var s = new QuaysideSettings();
        Assert.True(s.DemurrageCountStartDay);
        Assert.True(s.DemurrageCountEndDay);
        Assert.Equal(0, s.DemurrageDefaultFreeDays);
        Assert.True(s.EmptyReturnRequiresDischargeAndDo);
    }

    [Fact]
    public void Every_setting_is_described_and_assumptions_are_flagged()
    {
        var all = QuaysideSettings.Describe();
        Assert.Equal(typeof(QuaysideSettings).GetProperties().Length, all.Count);
        Assert.Contains(all, x => x.Property.Name == nameof(QuaysideSettings.DemurrageCountStartDay) && x.Meta.IsAssumption);
        Assert.All(all, x => Assert.False(string.IsNullOrWhiteSpace(x.Meta.Description)));
    }

    [Fact]
    public void Settings_round_trip_through_database()
    {
        using var db = TestDatabase.CreateSqlite();
        using (var ctx = db.NewContext())
        {
            var s = SettingsStore.Load(ctx);
            s.DemurrageCountEndDay = false;
            s.OverweightFallbackKg = 30480;
            s.DemurrageRateFallbacks = "HighCube40=Dry40";
            SettingsStore.Save(ctx, s);
        }
        using (var ctx = db.NewContext(migrate: false))
        {
            var s = SettingsStore.Load(ctx);
            Assert.False(s.DemurrageCountEndDay);
            Assert.Equal(30480, s.OverweightFallbackKg);
            Assert.Equal("HighCube40=Dry40", s.DemurrageRateFallbacks);
            Assert.True(s.DemurrageCountStartDay);
        }
    }
}
