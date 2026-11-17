namespace Quayside.Excel;

/// <summary>The workbook layouts documented in docs/EXCEL_FORMATS.md.</summary>
public enum TemplateKind
{
    /// <summary>Container master list ("DefineCNTR").</summary>
    ContainerMaster = 1,
    /// <summary>Containers with cargo details for one bill of lading ("Read CNTRs").</summary>
    BlContainers = 2,
    /// <summary>Full return + attach to house B/L ("F.RTN + Add to BL").</summary>
    FullReturnAttach = 3,
    /// <summary>Release to shipper ("RTS").</summary>
    ReleaseToShipper = 4,
    /// <summary>Full return ("FRTN").</summary>
    FullReturn = 5,
    /// <summary>Empty return ("MT RTN").</summary>
    EmptyReturn = 6,
}

public enum ColumnType
{
    /// <summary>Text; written as a number when it is a plain integer (codes, B/L and booking numbers).</summary>
    Code,
    Text,
    Container,
    Int,
    Decimal,
    /// <summary>0/1 flag.</summary>
    Flag,
    /// <summary>Real Excel date with <see cref="ColumnSpec.Format"/>.</summary>
    Date,
    /// <summary>Date written as text with <see cref="ColumnSpec.Format"/> (accepts real dates on read).</summary>
    DateText,
}

/// <param name="Key">Stable internal key used by importer/exporter.</param>
/// <param name="Header">Header text written on export; matched trimmed/case-insensitively on import.</param>
public sealed record ColumnSpec(string Key, string Header, ColumnType Type, bool Required = false, string? Format = null);

public sealed record TemplateSpec(TemplateKind Kind, string DisplayName, string SuggestedFileName,
    IReadOnlyList<ColumnSpec> Columns, IReadOnlyList<string> ExtraEmptySheets)
{
    public ColumnSpec Column(string key) => Columns.Single(c => c.Key == key);
}

public static class Templates
{
    private static ColumnSpec[] Cargo(string full, string seal, string soc, string movement, string cls, string un,
        string temp, string pkg, string gw, string nw, string cargo, string permit) => new[]
    {
        new ColumnSpec(Keys.Full, full, ColumnType.Flag),
        new ColumnSpec(Keys.Seal, seal, ColumnType.Text),
        new ColumnSpec(Keys.Soc, soc, ColumnType.Flag),
        new ColumnSpec(Keys.Movement, movement, ColumnType.Text),
        new ColumnSpec(Keys.DgClass, cls, ColumnType.Text),
        new ColumnSpec(Keys.UnNumber, un, ColumnType.Text),
        new ColumnSpec(Keys.ReeferTemp, temp, ColumnType.Decimal),
        new ColumnSpec(Keys.Packages, pkg, ColumnType.Int),
        new ColumnSpec(Keys.Gross, gw, ColumnType.Decimal),
        new ColumnSpec(Keys.Net, nw, ColumnType.Decimal),
        new ColumnSpec(Keys.CargoType, cargo, ColumnType.Text),
        new ColumnSpec(Keys.ExportPermit, permit, ColumnType.Text),
    };

    public static readonly IReadOnlyDictionary<TemplateKind, TemplateSpec> All = new Dictionary<TemplateKind, TemplateSpec>
    {
        [TemplateKind.ContainerMaster] = new(TemplateKind.ContainerMaster, "Container master", "DefineCNTR.xlsx", new[]
        {
            new ColumnSpec(Keys.Code, "Code", ColumnType.Code),
            new ColumnSpec(Keys.Container, "CNTR", ColumnType.Container, Required: true),
            new ColumnSpec(Keys.Size, "Size", ColumnType.Int, Required: true),
            new ColumnSpec(Keys.Type, "Type", ColumnType.Text, Required: true),
            new ColumnSpec(Keys.Tare, "TW", ColumnType.Int),
            new ColumnSpec(Keys.Line, "Line", ColumnType.Text),
        }, Array.Empty<string>()),

        [TemplateKind.BlContainers] = new(TemplateKind.BlContainers, "Containers for a B/L", "Read CNTRs.xlsx",
            new[]
            {
                new ColumnSpec(Keys.Container, "CNTR", ColumnType.Container, Required: true),
                new ColumnSpec(Keys.Size, "Size", ColumnType.Int),
                new ColumnSpec(Keys.Type, "Type", ColumnType.Text),
                new ColumnSpec(Keys.Tare, "TW", ColumnType.Int),
            }.Concat(Cargo("Full", "Seal", "SOC", "MovementType", "Class", "DGCOde", "RfTemp", "NoOfPkg", "GW", "Nw",
                "CargoType", "ExpPer"))
             .Append(new ColumnSpec(Keys.Remark, "Remark", ColumnType.Text)).ToArray(),
            new[] { "Sheet2", "Sheet3" }),

        [TemplateKind.FullReturnAttach] = new(TemplateKind.FullReturnAttach, "Full return + attach to B/L",
            "F.RTN + Add to BL.xlsx",
            new[]
            {
                new ColumnSpec(Keys.Code, "Code", ColumnType.Code),
                new ColumnSpec(Keys.HouseBl, "H.BL", ColumnType.Code, Required: true),
                new ColumnSpec(Keys.Container, "Container No.", ColumnType.Container, Required: true),
                new ColumnSpec(Keys.Date, "F.RTN Date", ColumnType.DateText, Required: true, Format: "yyyy/MM/dd"),
            }.Concat(Cargo("Full", "Seal No", "SOC", "Movement Type", "Class", "DG Code", "RFTemp", "Pkg", "G.W", "N.W",
                "ST/DG/OG", "Exp Per")).ToArray(),
            Array.Empty<string>()),

        [TemplateKind.ReleaseToShipper] = new(TemplateKind.ReleaseToShipper, "Release to shipper (RTS)", "RTS.xlsx", new[]
        {
            new ColumnSpec(Keys.Code, "Code", ColumnType.Code),
            new ColumnSpec(Keys.Container, "Container No.", ColumnType.Container, Required: true),
            new ColumnSpec(Keys.Date, "Date", ColumnType.Date, Required: true, Format: "m/d/yyyy"),
            new ColumnSpec(Keys.Booking, "BookingNo", ColumnType.Code, Required: true),
        }, Array.Empty<string>()),

        [TemplateKind.FullReturn] = new(TemplateKind.FullReturn, "Full return (F.RTN)", "FRTN.xlsx", new[]
        {
            new ColumnSpec(Keys.Code, "Code", ColumnType.Code),
            new ColumnSpec(Keys.Container, "CNTR", ColumnType.Container, Required: true),
            new ColumnSpec(Keys.Date, "FRTN", ColumnType.Date, Required: true, Format: "m/d/yyyy"),
        }, Array.Empty<string>()),

        [TemplateKind.EmptyReturn] = new(TemplateKind.EmptyReturn, "Empty return (MT.RTN)", "MT RTN.xlsx", new[]
        {
            new ColumnSpec(Keys.Code, "Code", ColumnType.Code),
            new ColumnSpec(Keys.Container, "CNTR ", ColumnType.Container, Required: true),
            new ColumnSpec(Keys.Date, "MT", ColumnType.Date, Required: true, Format: "mm-dd-yy"),
            new ColumnSpec(Keys.BodyState, "Body", ColumnType.Int),
            new ColumnSpec(Keys.Depot, "Depot", ColumnType.Text),
        }, Array.Empty<string>()),
    };

    public static TemplateSpec Get(TemplateKind kind) => All[kind];

    public static string NormalizeHeader(string? h) =>
        string.Join(' ', (h ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    /// <summary>Picks the template whose required headers all appear and that matches the most headers.</summary>
    public static TemplateKind? Detect(IEnumerable<string?> headers)
    {
        var set = headers.Select(NormalizeHeader).Where(h => h.Length > 0).ToHashSet();
        return All.Values
            .Where(t => t.Columns.Where(c => c.Required).All(c => set.Contains(NormalizeHeader(c.Header))))
            .Select(t => (t.Kind, Score: t.Columns.Count(c => set.Contains(NormalizeHeader(c.Header))) * 100
                                         - (t.Columns.Count - set.Count) * (t.Columns.Count - set.Count)))
            .OrderByDescending(x => x.Score)
            .Select(x => (TemplateKind?)x.Kind)
            .FirstOrDefault();
    }
}

public static class Keys
{
    public const string Code = "code", Container = "cntr", Size = "size", Type = "type", Tare = "tare", Line = "line";
    public const string Full = "full", Seal = "seal", Soc = "soc", Movement = "movement", DgClass = "class";
    public const string UnNumber = "un", ReeferTemp = "rftemp", Packages = "pkg", Gross = "gw", Net = "nw";
    public const string CargoType = "cargo", ExportPermit = "permit", Remark = "remark", HouseBl = "hbl";
    public const string Date = "date", Booking = "booking", BodyState = "body", Depot = "depot";
}
