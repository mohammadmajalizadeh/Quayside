using Quayside.Domain.Entities;
using Quayside.Domain.Rules;

namespace Quayside.Tests.Domain;

public class ContainerNumberTests
{
    [Theory]
    [InlineData("CSQU3054383", 3)] // ISO 6346 reference example
    [InlineData("MSKU9070323", 3)]
    public void BR_C1_check_digit_matches_reference(string number, int expected) =>
        Assert.Equal(expected, ContainerNumber.ComputeCheckDigit(number[..10]));

    [Theory]
    [InlineData("csqu3054383", "CSQU3054383")]
    [InlineData("CSQU305438-3", "CSQU3054383")]
    [InlineData(" CSQU 305438 3 ", "CSQU3054383")]
    public void BR_C1_normalizes_case_and_separators(string raw, string expected) =>
        Assert.Equal(expected, ContainerNumber.Normalize(raw));

    [Fact]
    public void BR_C1_valid_number_has_no_issues()
    {
        Assert.Empty(ContainerNumber.Validate("CSQU305438-3", out var n));
        Assert.Equal("CSQU3054383", n);
    }

    [Theory]
    [InlineData("CSQU305438")]
    [InlineData("CSQ13054383")]
    [InlineData("")]
    public void BR_C1_bad_format_is_error(string raw) =>
        Assert.Contains(ContainerNumber.Validate(raw, out _), v => v.Severity == Severity.Error);

    [Fact]
    public void BR_C1_wrong_check_digit_is_warning_by_default_error_when_configured()
    {
        var w = Assert.Single(ContainerNumber.Validate("CSQU3054384", out _));
        Assert.Equal(Severity.Warning, w.Severity);
        Assert.Equal(Severity.Error, ContainerNumber.Validate("CSQU3054384", out _, checkDigitIsError: true).Single().Severity);
    }

    [Fact]
    public void BR_C1_non_standard_category_letter_is_warning_only()
    {
        var issues = ContainerNumber.Validate("TEST1234567", out _);
        Assert.False(issues.HasErrors());
        Assert.Contains(issues, i => i.Message.Contains("category"));
    }
}

public class ContainerRulesTests
{
    [Fact]
    public void BR_C3_standard_cargo_with_dg_data_is_rejected()
    {
        var c = new ContainerCycle { CargoType = CargoType.Standard, DgClass = "3" };
        Assert.Contains(ContainerRules.ValidateCargoType(c, null), v => v.Rule == "BR-C3");
    }

    [Fact]
    public void BR_C3_dg_cargo_without_dg_data_is_rejected()
    {
        var c = new ContainerCycle { CargoType = CargoType.Dangerous };
        Assert.Contains(ContainerRules.ValidateCargoType(c, null), v => v.Rule == "BR-C3");
    }

    [Fact]
    public void BR_C3_dg_cargo_with_un_number_is_accepted() =>
        Assert.Empty(ContainerRules.ValidateCargoType(new ContainerCycle { CargoType = CargoType.Dangerous, UnNumber = "1203" }, null));

    [Fact]
    public void BR_C4_overweight_requires_og_using_container_max_gross()
    {
        var box = new Container { MaxGrossKg = 30480 };
        var heavy = new ContainerCycle { GrossWeightKg = 31000, CargoType = CargoType.Standard };
        Assert.Contains(ContainerRules.ValidateCargoType(heavy, box), v => v.Rule == "BR-C4");
        heavy.CargoType = CargoType.OutOfGauge;
        Assert.Empty(ContainerRules.ValidateCargoType(heavy, box));
    }

    [Fact]
    public void BR_C4_fallback_threshold_used_only_when_container_has_no_max_gross()
    {
        var heavy = new ContainerCycle { GrossWeightKg = 26000 };
        Assert.Empty(ContainerRules.ValidateCargoType(heavy, new Container()));
        Assert.Single(ContainerRules.ValidateCargoType(heavy, new Container(), fallbackOverweightKg: 25000));
    }

    [Fact]
    public void BR_C2_owner_prefix_enforced_only_when_line_defines_prefixes()
    {
        Assert.Empty(ContainerRules.ValidateOwnerPrefix("CSQU3054383", new ShippingLine { Code = "L1" }));
        var line = new ShippingLine { Code = "L1", OwnerPrefixes = "msku, mscu" };
        Assert.Single(ContainerRules.ValidateOwnerPrefix("CSQU3054383", line));
        Assert.Empty(ContainerRules.ValidateOwnerPrefix("MSKU9070323", line));
    }

    [Fact]
    public void BR_C5_duplicates_detected() =>
        Assert.Equal(new[] { "CSQU3054383" },
            ContainerRules.FindDuplicates(new[] { "CSQU3054383", "MSKU9070323", "CSQU3054383" }));

    [Fact]
    public void BR_C8_size_or_type_mismatch_with_master_is_error()
    {
        var master = new Container { Number = "CSQU3054383", SizeFeet = 20, TypeCode = "DC" };
        Assert.Empty(ContainerRules.ValidateAgainstMaster(master, 20, "dc"));
        Assert.Equal(2, ContainerRules.ValidateAgainstMaster(master, 40, "HC").Count);
    }

    [Theory]
    [InlineData("st", CargoType.Standard)]
    [InlineData("", CargoType.Standard)]
    [InlineData("DG", CargoType.Dangerous)]
    [InlineData(" og ", CargoType.OutOfGauge)]
    public void Cargo_type_codes_parse(string code, CargoType expected) =>
        Assert.Equal(expected, ContainerRules.ParseCargoType(code));

    [Fact]
    public void Unknown_cargo_type_code_throws() =>
        Assert.Throws<FormatException>(() => ContainerRules.ParseCargoType("XX"));
}
