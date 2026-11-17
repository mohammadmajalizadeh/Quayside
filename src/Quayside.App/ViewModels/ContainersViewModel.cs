using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using Quayside.Domain.Rules;
using Quayside.Excel;

namespace Quayside.App.ViewModels;

public sealed record ContainerRow(string Number, int SizeFeet, string TypeCode, int? TareKg, int? MaxGrossKg, string? Line, int Cycles);

public sealed partial class ContainersViewModel(AppServices services) : ObservableObject
{
    [ObservableProperty] private string _search = "";

    public ObservableCollection<ContainerRow> Items { get; } = new();

    [RelayCommand]
    public void Refresh()
    {
        using var db = services.NewContext();
        var term = ContainerNumber.Normalize(Search);
        var rows = db.Containers.Include(c => c.Line)
            .Where(c => term == "" || c.Number.Contains(term))
            .OrderBy(c => c.Number).Take(1000)
            .Select(c => new ContainerRow(c.Number, c.SizeFeet, c.TypeCode, c.TareKg, c.MaxGrossKg, c.Line!.Code,
                db.ContainerCycles.Count(x => x.ContainerId == c.Id)))
            .ToList();
        Items.Clear();
        foreach (var r in rows) Items.Add(r);
    }

    [RelayCommand]
    private void Export()
    {
        var dlg = new SaveFileDialog { Filter = "Excel workbook (*.xlsx)|*.xlsx", FileName = Templates.Get(TemplateKind.ContainerMaster).SuggestedFileName };
        if (dlg.ShowDialog() != true) return;
        AppServices.Try(() =>
        {
            using var db = services.NewContext();
            using var fs = File.Create(dlg.FileName);
            new TemplateExporter(db).Export(TemplateKind.ContainerMaster, null, fs);
        });
    }
}
