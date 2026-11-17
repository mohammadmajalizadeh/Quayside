using ClosedXML.Excel;
using Quayside.Domain.Rules;
using Quayside.Excel;

namespace Quayside.Tests.Excel;

/// <summary>
/// Synthetic workbooks mimicking the documented layouts, built with raw ClosedXML (independent of
/// <see cref="SheetIO.Write"/>) so round-trip tests are meaningful. All data is invented.
/// </summary>
public static class ExcelFixtures
{
    /// <summary>Valid ISO 6346 number for a 10-character stem, e.g. "QSDU100001".</summary>
    public static string Num(string stem) => stem + ContainerNumber.ComputeCheckDigit(stem);

    /// <summary>Same number with a dash before the check digit, as some source files have it.</summary>
    public static string Dashed(string stem) => stem + "-" + ContainerNumber.ComputeCheckDigit(stem);

    public static MemoryStream Build(string[] headers, params object?[][] rows) => Build(headers, null, rows);

    public static MemoryStream Build(string[] headers, Action<IXLWorksheet>? style, params object?[][] rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                ws.Cell(r + 2, c + 1).Value = rows[r][c] switch
                {
                    null => Blank.Value,
                    DateTime d => d,
                    int i => i,
                    double d => d,
                    string s => s,
                    var o => o.ToString(),
                };
        style?.Invoke(ws);
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static void DateFormat(IXLWorksheet ws, string col, string fmt) =>
        ws.Column(col).Style.NumberFormat.Format = fmt;

    public static MemoryStream ContainerMaster() => Build(
        new[] { "Code", "CNTR", "Size", "Type", "TW", "Line" },
        new object?[] { 501, Num("QSDU100001"), 20, "DC", 2200, "QSL" },
        new object?[] { 501, Num("QSDU100002").ToLowerInvariant(), 40, "HC", 3900, "QSL" },
        new object?[] { 501, Num("QSDU100003"), 40, "RF", 4600, "QSL" },
        new object?[] { 501, Num("QSDU100004"), 45, "HC", 4800, null });

    public static MemoryStream Release() => Build(
        new[] { "Code", "Container No.", "Date", "BookingNo", "", "", "" },
        ws => DateFormat(ws, "C", "m/d/yyyy"),
        new object?[] { 7001, Dashed("QSDU100001"), new DateTime(2026, 2, 1), 90000001 },
        new object?[] { 7001, Dashed("QSDU100002"), new DateTime(2026, 2, 1), 90000001 },
        new object?[] { 7001, Dashed("QSDU100003"), new DateTime(2026, 2, 3), 90000002 });

    public static MemoryStream FullReturn() => Build(
        new[] { "Code", "CNTR", "FRTN" },
        ws => DateFormat(ws, "C", "m/d/yyyy"),
        new object?[] { 7002, Dashed("QSDU100001"), new DateTime(2026, 2, 6) },
        new object?[] { 7002, Dashed("QSDU100002"), new DateTime(2026, 2, 7) });

    public static readonly string[] AttachHeaders =
    {
        "Code", "H.BL", "Container No.", "F.RTN Date", "Full", "Seal No", "SOC", "Movement Type", "Class",
        "DG Code", "RFTemp", "Pkg", "G.W", "N.W", "ST/DG/OG", "Exp Per",
    };

    public static MemoryStream FullReturnAttach() => Build(AttachHeaders,
        new object?[] { "7003X", 880001, Num("QSDU100003"), "2026/02/09", 1, "QS0001", 0, "FCL", null, null, -18, 1200, 21500, 20000, "ST", null },
        new object?[] { "7003X", 880001, Num("QSDU100004"), "2026/02/09", 1, "QS0002", 1, "FCL", "3", "1203", null, 800, 18000.5, 17500, "DG", "EP-17" });

    public static readonly string[] BlContainerHeaders =
    {
        "CNTR", "Size", "Type", "TW", "Full", "Seal", "SOC", "MovementType", "Class", "DGCOde", "RfTemp", "NoOfPkg",
        "GW", "Nw", "CargoType", "ExpPer", "Remark",
    };

    public static MemoryStream BlContainers() => Build(BlContainerHeaders,
        new object?[] { Num("QSDU200001").ToLowerInvariant(), 20, "DC", 2200, 1, "-", 0, "FCL", null, null, null, 10, 12000, 11000, "ST", null, null },
        new object?[] { Num("QSDU200002"), 40, "RF", 4600, 1, "S-77", 0, "FCL", null, null, 4.5, 600, 24000, 22000, "ST", null, "keep cold" });

    public static MemoryStream EmptyReturn() => Build(
        new[] { "Code", "CNTR ", "MT", "Body", "Depot" },
        ws => DateFormat(ws, "C", "mm-dd-yy"),
        new object?[] { 7004, Dashed("QSDU200001"), new DateTime(2026, 3, 2), 1, "North Yard" },
        new object?[] { 7004, Dashed("QSDU200002"), new DateTime(2026, 3, 4), 3, "North Yard" });
}
