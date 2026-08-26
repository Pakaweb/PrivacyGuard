using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PrivacyGuard.Helpers;
using PrivacyGuard.ViewModels;

namespace PrivacyGuard.Views;

public sealed partial class DashboardPage : Page
{
    private readonly HashSet<ToggleSwitch> _armedToggles = [];
    private bool _suppressToggle;

    public DashboardViewModel ViewModel { get; }

    public DashboardPage(DashboardViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Attach();
        await ViewModel.RefreshAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => ViewModel.Detach();

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        ViewModel.SearchQuery = sender.Text ?? string.Empty;
    }

    private void SettingCard_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border card)
        {
            return;
        }

        EnsureHoverMotion(card);
        card.BorderBrush = new SolidColorBrush(ThemePalette.Accent(80, 72));
        card.Background = new SolidColorBrush(ThemePalette.Overlay(22, 12));
        card.Translation = new System.Numerics.Vector3(0, -2, 12);
    }

    private void SettingCard_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border card)
        {
            return;
        }

        card.ClearValue(Border.BorderBrushProperty);
        card.ClearValue(Border.BackgroundProperty);
        card.Translation = new System.Numerics.Vector3(0, 0, 0);
    }

    private static void EnsureHoverMotion(UIElement element)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        var visual = ElementCompositionPreview.GetElementVisual(element);
        if (visual.ImplicitAnimations is not null)
        {
            return;
        }

        var compositor = visual.Compositor;
        var translation = compositor.CreateVector3KeyFrameAnimation();
        translation.Target = "Translation";
        translation.InsertExpressionKeyFrame(1f, "this.FinalValue");
        translation.Duration = TimeSpan.FromMilliseconds(180);

        var animations = compositor.CreateImplicitAnimationCollection();
        animations["Translation"] = translation;
        visual.ImplicitAnimations = animations;
    }

    private void PrivacyToggle_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle)
        {
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => _armedToggles.Add(toggle));
        }
    }

    private void PrivacyToggle_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle)
        {
            _armedToggles.Remove(toggle);
        }
    }

    private async void PrivacyToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggle || ViewModel.IsRebuilding || ViewModel.IsBusy)
        {
            return;
        }

        if (sender is not ToggleSwitch toggle || !_armedToggles.Contains(toggle))
        {
            return;
        }

        if (toggle.Tag is not PrivacyStatusItem item || toggle.IsOn == item.IsOn)
        {
            return;
        }

        var applied = await ViewModel.TrySetEnabledAsync(item, toggle.IsOn);
        if (applied)
        {
            return;
        }

        _suppressToggle = true;
        toggle.IsOn = item.IsOn;
        _suppressToggle = false;
    }
}
