using System.Globalization;
using Microsoft.UI.Xaml;
using PrivacyGuard.Helpers;
using PrivacyGuard.Models;
using Windows.Globalization;

namespace PrivacyGuard.Services;

/// <summary>
/// In-memory UI translations plus culture/RTL application for unpackaged WinUI.
/// </summary>
public sealed class LocalizationService : ObservableObject, ILocalizationService
{
    public static LocalizationService Current { get; private set; } = null!;

    public LocalizationService()
    {
        Current = this;
        CurrentCode = AppPreferences.DefaultLanguage;
    }

    public IReadOnlyList<LanguageOption> Languages => LocalizationCatalog.Languages;

    public string CurrentCode { get; private set; }

    [ObservableProperty]
    private int _stamp;

    public FlowDirection FlowDirection =>
        CurrentCode.Equals("ar", StringComparison.OrdinalIgnoreCase)
        || CurrentCode.StartsWith("ar-", StringComparison.OrdinalIgnoreCase)
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

    public event EventHandler? LanguageChanged;

    public LanguageOption Resolve(string? cultureName)
    {
        var normalized = LocalizationCatalog.Normalize(cultureName);
        return Languages.First(language => language.Code.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    public void Apply(string cultureName, bool notify = true)
    {
        var option = Resolve(cultureName);
        var culture = CreateCulture(option.Code);

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        try
        {
            ApplicationLanguages.PrimaryLanguageOverride = option.Code == "ar" ? "ar" : culture.Name;
        }
        catch (Exception)
        {
            // Unpackaged WinUI still honors CurrentUICulture for formatting.
        }

        var changed = !CurrentCode.Equals(option.Code, StringComparison.OrdinalIgnoreCase);
        CurrentCode = option.Code;

        if (!notify)
        {
            return;
        }

        Stamp++;
        OnPropertyChanged(nameof(FlowDirection));
        OnPropertyChanged(nameof(CurrentCode));
        OnPropertyChanged(string.Empty);
        if (changed)
        {
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string Get(string key, params object[] args)
    {
        var format = LocalizationCatalog.Get(CurrentCode, key);
        return args.Length == 0 ? format : string.Format(CultureInfo.CurrentCulture, format, args);
    }

    public string T(int stamp, string key) => Get(key);

    public string FormatProfileReason(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return Get("history.manualChange");
        }

        if (stored.StartsWith("Revert:", StringComparison.OrdinalIgnoreCase))
        {
            var key = stored[7..].Trim();
            return Get("privacy.revertReason", PrivacyCatalog.DisplayName(key));
        }

        if (stored.StartsWith("RestorePoint:", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(stored[13..], out var restoreId))
        {
            return Get("privacy.restoreReason", restoreId);
        }

        var hash = stored.LastIndexOf('#');
        if (hash >= 0 && int.TryParse(stored[(hash + 1)..], out var id))
        {
            return Get("privacy.restoreReason", id);
        }

        return stored switch
        {
            "Recommended Privacy" or "Recommended" => Get("profile.recommended.title"),
            "Maximum Privacy" or "Maximum" => Get("profile.maximum.title"),
            "Balanced" => Get("profile.balanced.title"),
            "Restore Default" or "RestoreDefault" => Get("profile.restore.title"),
            "WindowsReset" or "Windows reset" => Get("reset.historyName"),
            _ => stored
        };
    }

    private static CultureInfo CreateCulture(string code)
    {
        try
        {
            return CultureInfo.GetCultureInfo(code == "ar" ? "ar" : code);
        }
        catch (CultureNotFoundException)
        {
            return code == "ar"
                ? CultureInfo.GetCultureInfo("ar-SA")
                : CultureInfo.GetCultureInfo(AppPreferences.DefaultLanguage);
        }
    }
}
