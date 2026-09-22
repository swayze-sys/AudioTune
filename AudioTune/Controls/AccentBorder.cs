using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AudioTune.Controls;

public enum AccentEdge
{
    TopLeft,
    TopRight,
    BottomRight
}

/// <summary>Draws a soft corner light on the card surface and rounded outline.</summary>
public sealed class AccentBorder : Border
{
    public static readonly DependencyProperty AccentColorProperty = DependencyProperty.Register(
        nameof(AccentColor), typeof(Color), typeof(AccentBorder),
        new FrameworkPropertyMetadata(Color.FromRgb(113, 212, 255), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AccentEdgeProperty = DependencyProperty.Register(
        nameof(AccentEdge), typeof(AccentEdge), typeof(AccentBorder),
        new FrameworkPropertyMetadata(AccentEdge.TopLeft, FrameworkPropertyMetadataOptions.AffectsRender));

    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    public AccentEdge AccentEdge
    {
        get => (AccentEdge)GetValue(AccentEdgeProperty);
        set => SetValue(AccentEdgeProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (ActualWidth < 40 || ActualHeight < 24) return;

        var (x, y) = AccentEdge switch
        {
            AccentEdge.TopRight => (1.0, 0.0),
            AccentEdge.BottomRight => (1.0, 1.0),
            _ => (0.0, 0.0)
        };

        var glow = new RadialGradientBrush
        {
            Center = new Point(x, y),
            GradientOrigin = new Point(x, y),
            RadiusX = 0.48,
            RadiusY = 0.90
        };
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(35, AccentColor.R, AccentColor.G, AccentColor.B), 0));
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(12, AccentColor.R, AccentColor.G, AccentColor.B), 0.44));
        glow.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
        glow.Freeze();

        double inset = Math.Max(1, BorderThickness.Left);
        var inner = new Rect(inset, inset, ActualWidth - 2 * inset, ActualHeight - 2 * inset);
        double radius = Math.Max(0, CornerRadius.TopLeft - inset);
        drawingContext.DrawRoundedRectangle(glow, null, inner, radius, radius);

        var edge = new RadialGradientBrush
        {
            Center = new Point(x, y),
            GradientOrigin = new Point(x, y),
            RadiusX = 0.42,
            RadiusY = 0.80
        };
        edge.GradientStops.Add(new GradientStop(Color.FromArgb(215, AccentColor.R, AccentColor.G, AccentColor.B), 0));
        edge.GradientStops.Add(new GradientStop(Color.FromArgb(95, AccentColor.R, AccentColor.G, AccentColor.B), 0.32));
        edge.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
        edge.Freeze();

        var outline = new Rect(0.75, 0.75, ActualWidth - 1.5, ActualHeight - 1.5);
        double outlineRadius = Math.Max(0, CornerRadius.TopLeft - 0.75);
        var edgePen = new Pen(edge, 1.5);
        edgePen.Freeze();
        drawingContext.DrawRoundedRectangle(null, edgePen, outline, outlineRadius, outlineRadius);
    }
}