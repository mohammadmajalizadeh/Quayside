using System.Globalization;
using ClosedXML.Excel;
using Quayside.Domain.Rules;

namespace Quayside.Excel;

/// <summary>One data row of a template, values converted to .NET types per <see cref="ColumnType"/>.</summary>
public sealed class SheetRow
{
    public int RowNumber { get; init; }
    public Dictionary<string, object?> Values { get; } = new();
    public List<string> CellErrors { get; } = new();

    public string? Text(string key) => Values.GetValueOrDefault(key) as string;
    public int? Int(string key) => Values.GetValueOrDefault(key) as int?;
    public decimal? Decimal(string key) => Values.GetValueOrDefault(key) as decimal?;
    public bool? Flag(string key) => Values.GetValueOrDefault(key) as bool?;
    public DateOnly? Date(string key) => Values.GetValueOrDefault(key) as DateOnly?;
    public bool IsEmpty => Values.Values.All(v => v is null);
}

public sealed class SheetData
{
    public required TemplateSpec Template { get; init; }
    public List<SheetRow> Rows { get; } = new();
    public List<string> FileErrors { get; } = new();
    public List<string> FileWarnings { get; } = new();
}

/// <summary>Reads and writes template workbooks with ClosedXML.</summary>
public static class SheetIO
{
    private static readonly string[] TextDateFormats =
    {
        "yyyy/MM/dd", "yyyy/M/d", "yyyy-MM-dd", "yyyy-M-d", "M/d/yyyy", "MM/dd/yyyy", "d.M.yyyy", "dd.MM.yyyy",
    };

    public static TemplateKind? DetectKind(Stream xlsx)
    {
        using var wb = new XLWorkbook(xlsx);
        var ws = wb.Worksheets.First();
        var last = ws.Row(1).LastCellUsed()?.Address.ColumnNumber ?? 0;
        return Templates.Detect(Enumerable.Range(1, last).Select(c => ws.Cell(1, c).GetString()));
    }

    public static SheetData Read(Stream xlsx, TemplateKind kind)
    {
        var spec = Templates.Get(kind);
        var data = new SheetData { Template = spec };
        using var wb = new XLWorkbook(xlsx);
        var ws = wb.Worksheets.First();

        var lastCol = ws.Row(1).LastCellUsed()?.Address.ColumnNumber ?? 0;
        var colOf = new Dictionary<string, int>();
        for (var c = 1; c <= lastCol; c++)
        {
            var h = Templates.NormalizeHeader(ws.Cell(1, c).GetString());
            if (h.Length == 0) continue;
            var col = spec.Columns.FirstOrDefault(x => Templates.NormalizeHeader(x.Header) == h);
            if (col is null) data.FileWarnings.Add($"Column '{ws.Cell(1, c).GetString().Trim()}' is not part of the {spec.DisplayName} template and was ignored.");
            else if (!colOf.TryAdd(col.Key, c)) data.FileErrors.Add($"Column '{col.Header.Trim()}' appears twice.");
        }
        foreach (var missing in spec.Columns.Where(c => c.Required && !colOf.ContainsKey(c.Key)))
            data.FileErrors.Add($"Required column '{missing.Header.Trim()}' is missing.");
        if (data.FileErrors.Count > 0) return data;

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastRow; r++)
        {
            var row = new SheetRow { RowNumber = r };
            foreach (var col in spec.Columns)
            {
                if (!colOf.TryGetValue(col.Key, out var c)) { row.Values[col.Key] = null; continue; }
                try { row.Values[col.Key] = Convert(ws.Cell(r, c).Value, col.Type); }
                catch (FormatException ex)
                {
                    row.Values[col.Key] = null;
                    row.CellErrors.Add($"{col.Header.Trim()}: {ex.Message}");
                }
            }
            if (!row.IsEmpty || row.CellErrors.Count > 0) data.Rows.Add(row);
        }
        return data;
    }

    public static object? Convert(XLCellValue v, ColumnType type)
    {
        if (v.IsBlank) return null;
        if (v.IsText && string.IsNullOrWhiteSpace(v.GetText())) return null;
        switch (type)
        {
            case ColumnType.Code:
            case ColumnType.Text:
                return AsText(v);
            case ColumnType.Container:
                return ContainerNumber.Normalize(AsText(v));
            case ColumnType.Int:
                var d = AsDecimal(v);
                if (d != decimal.Truncate(d)) throw new FormatException($"'{AsText(v)}' is not a whole number.");
                return (int)d;
            case ColumnType.Decimal:
                return AsDecimal(v);
            case ColumnType.Flag:
                return AsText(v).ToUpperInvariant() switch
                {
                    "1" or "TRUE" or "Y" or "YES" => true,
                    "0" or "FALSE" or "N" or "NO" => false,
                    var s => throw new FormatException($"'{s}' is not 0/1."),
                };
            case ColumnType.Date:
            case ColumnType.DateText:
                return AsDate(v);
            default:
                throw new NotSupportedException(type.ToString());
        }
    }

    private static string AsText(XLCellValue v) => v.Type switch
    {
        XLDataType.Number => v.GetNumber().ToString("0.############", CultureInfo.InvariantCulture),
        XLDataType.DateTime => v.GetDateTime().ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
        XLDataType.Boolean => v.GetBoolean() ? "1" : "0",
        XLDataType.Error => throw new FormatException("cell contains an Excel error"),
        _ => v.ToString(CultureInfo.InvariantCulture).Trim(),
    };

    private static decimal AsDecimal(XLCellValue v)
    {
        if (v.IsNumber) return (decimal)v.GetNumber();
        var s = AsText(v);
        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) return d;
        throw new FormatException($"'{s}' is not a number.");
    }

    private static DateOnly AsDate(XLCellValue v)
    {
        if (v.IsDateTime) return DateOnly.FromDateTime(v.GetDateTime());
        if (v.IsNumber) return DateOnly.FromDateTime(DateTime.FromOADate(v.GetNumber()));
        var s = AsText(v);
        if (DateTime.TryParseExact(s, TextDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return DateOnly.FromDateTime(dt);
        throw new FormatException($"'{s}' is not a date (use yyyy/mm/dd).");
    }

    /// <summary>Writes rows (key → value) in the template's exact layout.</summary>
    public static XLWorkbook Write(TemplateKind kind, IEnumerable<IReadOnlyDictionary<string, object?>> rows)
    {
        var spec = Templates.Get(kind);
        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        for (var c = 0; c < spec.Columns.Count; c++)
            ws.Cell(1, c + 1).Value = spec.Columns[c].Header;

        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < spec.Columns.Count; c++)
            {
                var col = spec.Columns[c];
                var cell = ws.Cell(r, c + 1);
                WriteCell(cell, col, row.GetValueOrDefault(col.Key));
            }
            r++;
        }
        foreach (var extra in spec.ExtraEmptySheets) wb.Worksheets.Add(extra);
        ws.Columns(1, spec.Columns.Count).AdjustToContents();
        return wb;
    }

    private static void WriteCell(IXLCell cell, ColumnSpec col, object? value)
    {
        switch (value)
        {
            case null:
                return;
            case DateOnly d when col.Type == ColumnType.DateText:
                cell.Value = d.ToString(col.Format ?? "yyyy/MM/dd", CultureInfo.InvariantCulture);
                return;
            case DateOnly d:
                cell.Value = d.ToDateTime(TimeOnly.MinValue);
                cell.Style.NumberFormat.Format = col.Format ?? "yyyy-mm-dd";
                return;
            case bool b:
                cell.Value = b ? 1 : 0;
                return;
            case int i:
                cell.Value = i;
                return;
            case decimal m:
                cell.Value = (double)m;
                return;
            case string s when col.Type == ColumnType.Code && IsPlainInteger(s):
                cell.Value = double.Parse(s, CultureInfo.InvariantCulture);
                return;
            case string s:
                cell.Value = s;
                return;
            default:
                cell.Value = System.Convert.ToString(value, CultureInfo.InvariantCulture);
                return;
        }
    }

    /// <summary>Integers that survive a round trip through an Excel number (no leading zeros, ≤ 15 digits).</summary>
    public static bool IsPlainInteger(string s) =>
        s.Length is > 0 and <= 15 && s.All(char.IsAsciiDigit) && (s.Length == 1 || s[0] != '0');
}
