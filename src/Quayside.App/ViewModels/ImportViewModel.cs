using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Quayside.Excel;

namespace Quayside.App.ViewModels;

public sealed record ResultRow(int Row, string? Container, RowStatus Status, string Messages);

public sealed partial class ImportViewModel(AppServices services) : ObservableObject
{
    public IReadOnlyList<TemplateSpec> TemplateList { get; } = Templates.All.Values.ToList();
    public ObservableCollection<Lookup> Bills { get; } = new();
    public ObservableCollection<ResultRow> Results { get; } = new();

    [ObservableProperty] private string? _filePath;
    [ObservableProperty] private TemplateSpec? _template;
    [ObservableProperty] private Lookup? _targetBl;
    [ObservableProperty] private bool _backfill;
    [ObservableProperty] private bool _validRowsOnly;
    [ObservableProperty] private string _summary = "Choose a workbook, preview, then import.";
    [ObservableProperty] private string _exportBatchRefs = "";

    [RelayCommand]
    public void Refresh()
    {
        using var db = services.NewContext();
        Bills.Clear();
        foreach (var b in db.BillsOfLading.OrderByDescending(b => b.Id).Take(500).Select(b => new Lookup(b.Id, b.HouseNo)))
            Bills.Add(b);
    }

    [RelayCommand]
    private void Browse()
    {
        var dlg = new OpenFileDialog { Filter = "Excel workbook (*.xlsx)|*.xlsx" };
        if (dlg.ShowDialog() != true) return;
        FilePath = dlg.FileName;
        AppServices.Try(() =>
        {
            using var fs = File.OpenRead(dlg.FileName);
            var kind = SheetIO.DetectKind(fs);
            Template = kind is { } k ? Templates.Get(k) : null;
            Summary = kind is null ? "Template not recognised; pick it manually." : $"Detected: {Template!.DisplayName}";
        });
    }

    [RelayCommand]
    private void Preview() => Run(commit: false);

    [RelayCommand]
    private void Import() => Run(commit: true);

    private void Run(bool commit)
    {
        if (FilePath is null || Template is null) return;
        AppServices.Try(() =>
        {
            using var db = services.NewContext();
            using var fs = File.OpenRead(FilePath);
            var report = new TemplateImporter(db).Import(fs, Template.Kind, new ImportOptions
            {
                Commit = commit, Backfill = Backfill, ImportValidRowsOnly = ValidRowsOnly, BillOfLadingId = TargetBl?.Id,
            });
            Results.Clear();
            foreach (var e in report.FileErrors) Results.Add(new ResultRow(0, null, RowStatus.Error, e));
            foreach (var w in report.FileWarnings) Results.Add(new ResultRow(0, null, RowStatus.Warning, w));
            foreach (var r in report.Rows) Results.Add(new ResultRow(r.RowNumber, r.Container, r.Status, string.Join(" | ", r.Messages)));
            var verb = report.Committed ? "Imported" : commit ? "Nothing saved:" : "Preview:";
            Summary = $"{verb} {report.ImportedRows} ok/warning row(s), {report.ErrorRows} error row(s), {report.FileErrors.Count} file error(s).";
        });
    }

    [RelayCommand]
    private void Export()
    {
        if (Template is null) return;
        var dlg = new SaveFileDialog { Filter = "Excel workbook (*.xlsx)|*.xlsx", FileName = Template.SuggestedFileName };
        if (dlg.ShowDialog() != true) return;
        var refs = ExportBatchRefs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        AppServices.Try(() =>
        {
            using var db = services.NewContext();
            using var fs = File.Create(dlg.FileName);
            new TemplateExporter(db).Export(Template.Kind,
                new ExportFilter(refs.Length == 0 ? null : refs, TargetBl?.Id), fs);
            Summary = $"Exported to {dlg.FileName}";
        });
    }
}
