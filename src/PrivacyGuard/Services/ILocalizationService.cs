using System.ComponentModel;
using Microsoft.UI.Xaml;
using PrivacyGuard.Models;

namespace PrivacyGuard.Services;

public interface ILocalizationService : INotifyPropertyChanged
{
    IReadOnlyList<LanguageOption> Languages { get; }

    string CurrentCode { get; }

    int Stamp { get; }

    FlowDirection FlowDirection { get; }

    event EventHandler? LanguageChanged;

    LanguageOption Resolve(string? cultureName);

    void Apply(string cultureName, bool notify = true);

    string Get(string key, params object[] args);

    /// <summary>
    /// x:Bind helper. Pass <see cref="Stamp"/> so bindings refresh when the language changes.
    /// </summary>
    string T(int stamp, string key);

    string FormatProfileReason(string? stored);
}
