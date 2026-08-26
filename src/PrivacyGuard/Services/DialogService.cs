using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PrivacyGuard.Services;

/// <inheritdoc />
public sealed class DialogService : IDialogService
{
    public XamlRoot? XamlRoot { get; set; }

    public async Task<bool> ConfirmAsync(string title, string message, string? warning = null, string? primaryText = null)
    {
        var loc = LocalizationService.Current;
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap
        });

        if (!string.IsNullOrWhiteSpace(warning))
        {
            panel.Children.Add(new InfoBar
            {
                IsOpen = true,
                IsClosable = false,
                Severity = InfoBarSeverity.Warning,
                Title = loc.Get("dialog.beforeContinue"),
                Message = warning
            });
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = string.IsNullOrWhiteSpace(primaryText) ? loc.Get("common.apply") : primaryText,
            CloseButtonText = loc.Get("common.cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = RequireXamlRoot()
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    public async Task ShowMessageAsync(string title, string message, InfoBarSeverity severity = InfoBarSeverity.Informational)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = LocalizationService.Current.Get("common.ok"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = RequireXamlRoot()
        };

        if (severity == InfoBarSeverity.Error)
        {
            dialog.RequestedTheme = ElementTheme.Default;
        }

        await dialog.ShowAsync();
    }

    public Task ShowErrorAsync(string title, string message) =>
        ShowMessageAsync(title, message, InfoBarSeverity.Error);

    public async Task<ExportOptions?> ShowExportOptionsAsync()
    {
        var loc = LocalizationService.Current;
        var includeHistory = new CheckBox
        {
            Content = loc.Get("profiles.exportIncludeHistory"),
            IsChecked = false
        };

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = loc.Get("profiles.exportBody"),
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(includeHistory);

        var dialog = new ContentDialog
        {
            Title = loc.Get("profiles.exportTitle"),
            Content = panel,
            PrimaryButtonText = loc.Get("common.export"),
            CloseButtonText = loc.Get("common.cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = RequireXamlRoot()
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary
            ? new ExportOptions { IncludeHistoryAndRestorePoints = includeHistory.IsChecked == true }
            : null;
    }

    public async Task<ImportSelection?> ShowImportOptionsAsync(PrivacyGuardExport package)
    {
        var loc = LocalizationService.Current;
        var profileCount = package.CustomProfiles.Count;
        var historyCount = package.History?.Count ?? 0;
        var restoreCount = package.RestorePoints?.Count ?? 0;

        var profiles = new CheckBox
        {
            Content = loc.Get("profiles.importProfiles", profileCount),
            IsChecked = profileCount > 0,
            IsEnabled = profileCount > 0
        };
        var history = new CheckBox
        {
            Content = loc.Get("profiles.importHistory", historyCount),
            IsChecked = false,
            IsEnabled = historyCount > 0
        };
        var restore = new CheckBox
        {
            Content = loc.Get("profiles.importRestore", restoreCount),
            IsChecked = false,
            IsEnabled = restoreCount > 0
        };

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = loc.Get("profiles.importBody"),
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(profiles);
        panel.Children.Add(history);
        panel.Children.Add(restore);

        var dialog = new ContentDialog
        {
            Title = loc.Get("profiles.importTitle"),
            Content = panel,
            PrimaryButtonText = loc.Get("common.import"),
            CloseButtonText = loc.Get("common.cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = RequireXamlRoot()
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        return new ImportSelection
        {
            CustomProfiles = profiles.IsChecked == true,
            History = history.IsChecked == true,
            RestorePoints = restore.IsChecked == true
        };
    }

    public async Task<bool> ShowFirstRunAsync()
    {
        var loc = LocalizationService.Current;
        var panel = new StackPanel { Spacing = 12, MaxWidth = 560 };
        panel.Children.Add(new ScrollViewer
        {
            MaxHeight = 320,
            Content = new TextBlock
            {
                Text = loc.Get("onboarding.body"),
                TextWrapping = TextWrapping.Wrap
            }
        });
        panel.Children.Add(new InfoBar
        {
            IsOpen = true,
            IsClosable = false,
            Severity = InfoBarSeverity.Warning,
            Title = loc.Get("dialog.beforeContinue"),
            Message = loc.Get("profile.maximum.warning")
        });

        var dialog = new ContentDialog
        {
            Title = loc.Get("onboarding.title"),
            Content = panel,
            PrimaryButtonText = loc.Get("onboarding.accept"),
            CloseButtonText = loc.Get("onboarding.quit"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = RequireXamlRoot()
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private XamlRoot RequireXamlRoot()
    {
        if (XamlRoot is not null)
        {
            return XamlRoot;
        }

        if (App.MainWindow?.Content is FrameworkElement element && element.XamlRoot is not null)
        {
            XamlRoot = element.XamlRoot;
            return XamlRoot;
        }

        throw new InvalidOperationException("Dialogs cannot be shown until the main window is loaded.");
    }
}
