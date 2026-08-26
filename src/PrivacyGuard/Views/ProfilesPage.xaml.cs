using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PrivacyGuard.Helpers;
using PrivacyGuard.ViewModels;

namespace PrivacyGuard.Views;

public sealed partial class ProfilesPage : Page
{
    private static readonly System.Numerics.Vector3 RestTranslation = new(0, 0, 0);
    private static readonly System.Numerics.Vector3 HoverTranslation = new(0, -5, 32);

    public ProfilesViewModel ViewModel { get; }

    public ProfilesPage(ProfilesViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshPresentationAsync();
    }

    private void ProfileCard_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border card)
        {
            return;
        }

        EnsureHoverMotion(card);
        card.Background = new SolidColorBrush(ThemePalette.Overlay(40, 16));
        card.Translation = HoverTranslation;
    }

    private void ProfileCard_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border card)
        {
            return;
        }

        card.ClearValue(Border.BackgroundProperty);
        card.Translation = RestTranslation;
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
        translation.Duration = TimeSpan.FromMilliseconds(220);

        var animations = compositor.CreateImplicitAnimationCollection();
        animations["Translation"] = translation;
        visual.ImplicitAnimations = animations;

        element.Shadow ??= new ThemeShadow();
    }
}
