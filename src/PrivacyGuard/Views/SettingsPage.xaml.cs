using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using PrivacyGuard.Helpers;
using PrivacyGuard.ViewModels;

namespace PrivacyGuard.Views;

public sealed partial class SettingsPage : Page
{
    private static readonly System.Numerics.Vector3 RestTranslation = new(0, 0, 0);
    private static readonly System.Numerics.Vector3 HoverTranslation = new(0, -2, 16);

    public SettingsViewModel ViewModel { get; }

    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Loaded += (_, _) => ViewModel.AllowLanguageChanges();
    }

    private void SettingRow_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border row)
        {
            return;
        }

        EnsureHoverMotion(row);
        row.Background = new SolidColorBrush(ThemePalette.Overlay(36, 14));
        row.Translation = HoverTranslation;
        SetAccentOpacity(row, 1);
    }

    private void SettingRow_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border row)
        {
            return;
        }

        row.ClearValue(Border.BackgroundProperty);
        row.Translation = RestTranslation;
        SetAccentOpacity(row, 0);
    }

    private static void SetAccentOpacity(Border row, double opacity)
    {
        if (row.Child is Grid grid && grid.Children.Count > 0 && grid.Children[0] is Rectangle accent)
        {
            accent.Opacity = opacity;
        }
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
}
