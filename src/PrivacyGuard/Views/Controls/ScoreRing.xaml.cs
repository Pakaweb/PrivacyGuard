using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using PrivacyGuard.Helpers;

namespace PrivacyGuard.Views.Controls;

/// <summary>
/// Determinate privacy-score ring with a smooth red → green stroke.
/// </summary>
public sealed partial class ScoreRing : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(ScoreRing),
        new PropertyMetadata(0d, OnValueChanged));

    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
        nameof(Caption),
        typeof(string),
        typeof(ScoreRing),
        new PropertyMetadata("out of 100", OnCaptionChanged));

    public ScoreRing()
    {
        InitializeComponent();
        CaptionText.Text = Caption;
        Loaded += (_, _) => Redraw();
        SizeChanged += (_, _) => Redraw();
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ScoreRing)d).Redraw();

    private static void OnCaptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScoreRing ring)
        {
            ring.CaptionText.Text = e.NewValue as string ?? string.Empty;
        }
    }

    private void Redraw()
    {
        var score = Math.Clamp(Value, 0, 100);
        ValueText.Text = double.IsNaN(score) ? "—" : Math.Round(score).ToString("0");

        var brush = new SolidColorBrush(ScorePalette.ColorForScore(score / 100d));
        ProgressPath.Stroke = brush;
        ValueText.Foreground = brush;

        const double size = 168;
        const double stroke = 12;
        const double radius = (size - stroke) / 2;
        const double cx = size / 2;
        const double cy = size / 2;

        var progress = score / 100d;
        if (progress <= 0.004)
        {
            ProgressPath.Data = null;
            return;
        }

        if (progress >= 0.999)
        {
            var figure = FullCircle(cx, cy, radius);
            ProgressPath.Data = new PathGeometry { Figures = { figure } };
            return;
        }

        var start = -Math.PI / 2;
        var sweep = progress * 2 * Math.PI;
        var end = start + sweep;

        var figureArc = new PathFigure
        {
            StartPoint = PointOnCircle(cx, cy, radius, start),
            IsClosed = false,
            IsFilled = false
        };
        figureArc.Segments.Add(new ArcSegment
        {
            Point = PointOnCircle(cx, cy, radius, end),
            Size = new Size(radius, radius),
            IsLargeArc = sweep > Math.PI,
            SweepDirection = SweepDirection.Clockwise,
            RotationAngle = 0
        });

        ProgressPath.Data = new PathGeometry { Figures = { figureArc } };
    }

    private static PathFigure FullCircle(double cx, double cy, double radius)
    {
        var start = PointOnCircle(cx, cy, radius, -Math.PI / 2);
        var figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = PointOnCircle(cx, cy, radius, Math.PI / 2),
            Size = new Size(radius, radius),
            IsLargeArc = true,
            SweepDirection = SweepDirection.Clockwise
        });
        figure.Segments.Add(new ArcSegment
        {
            Point = start,
            Size = new Size(radius, radius),
            IsLargeArc = true,
            SweepDirection = SweepDirection.Clockwise
        });
        return figure;
    }

    private static Point PointOnCircle(double cx, double cy, double radius, double radians) =>
        new(cx + (radius * Math.Cos(radians)), cy + (radius * Math.Sin(radians)));
}

