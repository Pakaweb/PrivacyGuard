using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using PrivacyGuard.ViewModels;

namespace PrivacyGuard.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.AllowPreferenceChanges();
        LanguageFlyout.Items.Clear();
        foreach (var language in ViewModel.Languages)
        {
            var item = new MenuFlyoutItem
            {
                Text = language.NativeName,
                Tag = language
            };
            item.Click += LanguageItem_Click;
            LanguageFlyout.Items.Add(item);
        }
    }

    private void LanguageItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: PrivacyGuard.Models.LanguageOption language })
        {
            ViewModel.SelectedLanguage = language;
        }
    }

    private void SettingRow_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border row)
        {
            SetAccentOpacity(row, 1);
        }
    }

    private void SettingRow_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border row)
        {
            SetAccentOpacity(row, 0);
        }
    }

    private static void SetAccentOpacity(Border row, double opacity)
    {
        if (row.Child is Grid grid && grid.Children.Count > 0 && grid.Children[0] is Rectangle accent)
        {
            accent.Opacity = opacity;
        }
    }
}
