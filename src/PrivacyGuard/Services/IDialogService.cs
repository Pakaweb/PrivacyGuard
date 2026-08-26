using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PrivacyGuard.Services;

public interface IDialogService
{
    XamlRoot? XamlRoot { get; set; }

    Task<bool> ConfirmAsync(string title, string message, string? warning = null, string? primaryText = null);

    Task ShowMessageAsync(string title, string message, InfoBarSeverity severity = InfoBarSeverity.Informational);

    Task ShowErrorAsync(string title, string message);

    Task<ExportOptions?> ShowExportOptionsAsync();

    Task<ImportSelection?> ShowImportOptionsAsync(PrivacyGuardExport package);

    Task<bool> ShowFirstRunAsync();
}
