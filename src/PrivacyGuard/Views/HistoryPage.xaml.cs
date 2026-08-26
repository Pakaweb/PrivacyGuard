using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PrivacyGuard.Helpers;
using PrivacyGuard.ViewModels;

namespace PrivacyGuard.Views;

public sealed partial class HistoryPage : Page
{
    private static readonly System.Numerics.Vector3 RestTranslation = new(0, 0, 0);
    private static readonly System.Numerics.Vector3 HoverTranslation = new(0, -4, 24);
    private static readonly System.Numerics.Vector3 RestoreHoverTranslation = new(0, -1, 8);

    public HistoryViewModel ViewModel { get; }

    public HistoryPage(HistoryViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await ViewModel.RefreshAsync();
    }

    private void HistoryRow_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border row)
        {
            return;
        }

        EnsureHoverMotion(row);
        var restore = row.Tag is HistoryRestoreItem;
        var selected = IsRowSelected(row.Tag);
        row.Background = new SolidColorBrush(
            selected
                ? ThemePalette.Accent(restore ? (byte)62 : (byte)44, restore ? (byte)48 : (byte)36)
                : ThemePalette.Overlay(restore ? (byte)28 : (byte)48, restore ? (byte)14 : (byte)18));
        row.Translation = restore ? RestoreHoverTranslation : HoverTranslation;
    }

    private void HistoryRow_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border row)
        {
            return;
        }

        row.ClearValue(Border.BackgroundProperty);
        row.Translation = RestTranslation;
    }

    private static bool IsRowSelected(object? tag) =>
        tag is HistoryChangeItem change && change.IsSelected
        || tag is HistoryRestoreItem restore && restore.IsSelected;

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
        translation.Duration = TimeSpan.FromMilliseconds(220);

        var animations = compositor.CreateImplicitAnimationCollection();
        animations["Translation"] = translation;
        visual.ImplicitAnimations = animations;
        element.Shadow ??= new ThemeShadow();
    }
}
