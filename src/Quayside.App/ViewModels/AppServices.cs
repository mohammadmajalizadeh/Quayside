using System.Windows;
using Microsoft.EntityFrameworkCore;
using Quayside.Data;
using Quayside.Domain.Rules;
using Quayside.Domain.Settings;

namespace Quayside.App.ViewModels;

/// <summary>Shared services: short-lived DbContexts, settings and user feedback.</summary>
public sealed class AppServices
{
    public DatabaseOptions Database { get; }

    public AppServices(DatabaseOptions database)
    {
        Database = database;
        using var db = NewContext();
        db.Database.Migrate();
    }

    public QuaysideDbContext NewContext() => Database.CreateContext();

    public QuaysideSettings LoadSettings()
    {
        using var db = NewContext();
        return SettingsStore.Load(db);
    }

    /// <summary>Runs an action and reports rule violations / errors in a dialog. Returns false on failure.</summary>
    public static bool Try(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (BusinessRuleException ex)
        {
            MessageBox.Show(string.Join(Environment.NewLine, ex.Violations.Select(v => $"[{v.Rule}] {v.Message}")),
                "Not allowed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex) when (ex is DbUpdateException or FormatException or InvalidOperationException or IOException)
        {
            MessageBox.Show(ex.GetBaseException().Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        return false;
    }

    public static DateOnly? ToDate(DateTime? d) => d is { } x ? DateOnly.FromDateTime(x) : null;
    public static DateTime? ToDateTime(DateOnly? d) => d?.ToDateTime(TimeOnly.MinValue);
}
