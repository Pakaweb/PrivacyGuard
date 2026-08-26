using Microsoft.UI.Xaml;

namespace PrivacyGuard.Models;

/// <summary>
/// Persisted application preferences (not Windows privacy settings).
/// </summary>
public sealed class AppPreferences
{
    public const string DefaultLanguage = "en-US";

    public ElementTheme Theme { get; set; } = ElementTheme.Default;

    public string Language { get; set; } = DefaultLanguage;

    public bool StartWithWindows { get; set; }

    public bool ConfirmBeforeApply { get; set; } = true;

    public bool RecordHistory { get; set; } = true;

    /// <summary>Show a notification area icon while PrivacyGuard is running.</summary>
    public bool EnableTray { get; set; } = true;

    /// <summary>Hide the main window instead of exiting when the close button is used.</summary>
    public bool CloseToTray { get; set; } = true;

    /// <summary>Compare Windows privacy settings to the last applied PrivacyGuard state.</summary>
    public bool CheckForWindowsResets { get; set; } = true;

    /// <summary>When true, periodic reset checks are skipped (tray Pause monitoring).</summary>
    public bool MonitoringPaused { get; set; }

    /// <summary>Set after the first-run safety dialog is accepted.</summary>
    public bool HasSeenFirstRun { get; set; }
}
