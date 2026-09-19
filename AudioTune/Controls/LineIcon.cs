using System.Windows;
using System.Windows.Media;

namespace AudioTune.Controls;

public enum LineIconKind
{
    Home,
    Headphones,
    User,
    Speaker,
    Gear,
    Compare,
    Terminal,
    Refresh,
    Alert,
    Power,
    Minimize,
    Maximize,
    Close,
    Play,
    PlayCircle,
    Check,
    CheckCircle,
    Measurement,
    Link,
    Sliders,
    Target,
    Shield,
    BarChart,
    Waveform,
    Balance,
    ChevronDown,
    Info,
    Edit,
    Copy,
    Export,
    Import,
    Archive,
    Restore,
    Plus,
    ArrowRight,
    Processor,
    Pause,
    Stop,
    ExternalLink
}

public sealed class LineIcon : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(LineIconKind), typeof(LineIcon),
        new FrameworkPropertyMetadata(LineIconKind.Check, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(LineIcon),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(LineIcon),
        new FrameworkPropertyMetadata(1.7, FrameworkPropertyMetadataOptions.AffectsRender));

    public LineIconKind Kind
    {
        get => (LineIconKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) ? 18 : availableSize.Width;
        double height = double.IsInfinity(availableSize.Height) ? 18 : availableSize.Height;
        return new Size(Math.Min(width, 18), Math.Min(height, 18));
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        var pen = new Pen(Stroke, StrokeThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        switch (Kind)
        {
            case LineIconKind.Home:
                DrawPolyline(dc, pen, [(2, 8.2), (9, 2.2), (16, 8.2)]);
                DrawPolyline(dc, pen, [(3.8, 7.1), (3.8, 15.7), (14.2, 15.7), (14.2, 7.1)]);
                dc.DrawLine(pen, P(7.1, 15.7), P(7.1, 11.1));
                dc.DrawLine(pen, P(7.1, 11.1), P(10.9, 11.1));
                dc.DrawLine(pen, P(10.9, 11.1), P(10.9, 15.7));
                break;
            case LineIconKind.Headphones:
                DrawArc(dc, pen, P(9, 9), X(6.6), Y(6.6), 200, 140);
                dc.DrawRoundedRectangle(null, pen, R(1.9, 8.4, 3.2, 6.1), X(1.2), Y(1.2));
                dc.DrawRoundedRectangle(null, pen, R(12.9, 8.4, 3.2, 6.1), X(1.2), Y(1.2));
                break;
            case LineIconKind.User:
                dc.DrawEllipse(null, pen, P(9, 5.1), X(3.1), Y(3.1));
                DrawUserShoulders(dc, pen);
                break;
            case LineIconKind.Speaker:
                DrawPolygon(dc, null, pen, [(2.2, 7), (5.2, 7), (9.2, 3.6), (9.2, 14.4), (5.2, 11), (2.2, 11)]);
                DrawArc(dc, pen, P(9.7, 9), X(4), Y(4), -52, 104);
                DrawArc(dc, pen, P(9.7, 9), X(6.4), Y(6.4), -48, 96);
                break;
            case LineIconKind.Gear:
                DrawGear(dc, pen);
                break;
            case LineIconKind.Compare:
                dc.DrawEllipse(null, pen, P(3.2, 9), X(1.7), Y(1.7));
                dc.DrawEllipse(null, pen, P(14.8, 9), X(1.7), Y(1.7));
                dc.DrawLine(pen, P(5.6, 6.4), P(12.4, 6.4));
                DrawPolyline(dc, pen, [(10.1, 4.2), (12.4, 6.4), (10.1, 8.6)]);
                dc.DrawLine(pen, P(12.4, 11.6), P(5.6, 11.6));
                DrawPolyline(dc, pen, [(7.9, 9.4), (5.6, 11.6), (7.9, 13.8)]);
                break;
            case LineIconKind.Terminal:
                DrawPolyline(dc, pen, [(2.5, 4.2), (7.1, 8.7), (2.5, 13.2)]);
                dc.DrawLine(pen, P(8.8, 13.3), P(15.5, 13.3));
                break;
            case LineIconKind.Refresh:
                DrawArc(dc, pen, P(9, 9), X(6.3), Y(6.3), -55, 250);
                DrawPolyline(dc, pen, [(12.7, 2.4), (15.4, 3.2), (14.7, 5.9)]);
                break;
            case LineIconKind.Alert:
                DrawPolygon(dc, null, pen, [(9, 1.8), (16.5, 15.5), (1.5, 15.5)]);
                dc.DrawLine(pen, P(9, 6), P(9, 10.5));
                dc.DrawEllipse(Stroke, null, P(9, 13), X(.7), Y(.7));
                break;
            case LineIconKind.Power:
                DrawArc(dc, pen, P(9, 9.5), X(6.2), Y(6.2), -47, 274);
                dc.DrawLine(pen, P(9, 1.4), P(9, 8.4));
                break;
            case LineIconKind.Minimize:
                dc.DrawLine(pen, P(3.5, 11), P(14.5, 11));
                break;
            case LineIconKind.Maximize:
                dc.DrawRoundedRectangle(null, pen, R(4, 4, 10, 10), X(.6), Y(.6));
                break;
            case LineIconKind.Close:
                dc.DrawLine(pen, P(4.2, 4.2), P(13.8, 13.8));
                dc.DrawLine(pen, P(13.8, 4.2), P(4.2, 13.8));
                break;
            case LineIconKind.Play:
                DrawPolygon(dc, Stroke, null, [(4, 2.5), (15, 9), (4, 15.5)]);
                break;
            case LineIconKind.PlayCircle:
                dc.DrawEllipse(null, pen, P(9, 9), X(7.2), Y(7.2));
                DrawPolygon(dc, Stroke, null, [(7.2, 5.6), (13, 9), (7.2, 12.4)]);
                break;
            case LineIconKind.Check:
                DrawPolyline(dc, pen, [(3, 9.2), (7.1, 13.1), (15.3, 4.7)]);
                break;
            case LineIconKind.CheckCircle:
                dc.DrawEllipse(null, pen, P(9, 9), X(7.2), Y(7.2));
                DrawPolyline(dc, pen, [(5.2, 9.1), (8, 11.8), (13.1, 6.5)]);
                break;
            case LineIconKind.Measurement:
                dc.DrawRoundedRectangle(null, pen, R(3, 1.8, 12, 14.4), X(1.2), Y(1.2));
                for (int i = 0; i < 3; i++)
                {
                    double y = 5.2 + i * 3.2;
                    dc.DrawEllipse(Stroke, null, P(6, y), X(.65), Y(.65));
                    dc.DrawLine(pen, P(8.2, y), P(12.6, y));
                }
                break;
            case LineIconKind.Link:
                DrawLink(dc, pen);
                break;
            case LineIconKind.Sliders:
                DrawSliders(dc, pen);
                break;
            case LineIconKind.Target:
                dc.DrawEllipse(null, pen, P(9, 9), X(6.8), Y(6.8));
                dc.DrawEllipse(null, pen, P(9, 9), X(3.4), Y(3.4));
                dc.DrawEllipse(Stroke, null, P(9, 9), X(1), Y(1));
                dc.DrawLine(pen, P(9, 1), P(9, 3));
                dc.DrawLine(pen, P(9, 15), P(9, 17));
                dc.DrawLine(pen, P(1, 9), P(3, 9));
                dc.DrawLine(pen, P(15, 9), P(17, 9));
                break;
            case LineIconKind.Shield:
                DrawShield(dc, pen);
                break;
            case LineIconKind.BarChart:
                dc.DrawRectangle(Stroke, null, R(2.3, 9.5, 3.1, 6.5));
                dc.DrawRectangle(Stroke, null, R(7.45, 3.2, 3.1, 12.8));
                dc.DrawRectangle(Stroke, null, R(12.6, 6.8, 3.1, 9.2));
                break;
            case LineIconKind.Waveform:
                DrawPolyline(dc, pen, [(1, 9), (4.2, 9), (6.2, 3.2), (9.1, 15), (11.8, 6.1), (14, 10.5), (17, 10.5)]);
                break;
            case LineIconKind.Balance:
                DrawBalance(dc, pen);
                break;
            case LineIconKind.ChevronDown:
                DrawPolyline(dc, pen, [(3.5, 6.5), (9, 12), (14.5, 6.5)]);
                break;
            case LineIconKind.Info:
                dc.DrawEllipse(null, pen, P(9, 9), X(7), Y(7));
                dc.DrawEllipse(Stroke, null, P(9, 5.1), X(.75), Y(.75));
                dc.DrawLine(pen, P(9, 8), P(9, 13));
                break;
            case LineIconKind.Edit:
                DrawPolyline(dc, pen, [(3, 15), (4.1, 10.8), (11.8, 3.1), (14.9, 6.2), (7.2, 13.9), (3, 15)]);
                dc.DrawLine(pen, P(10.3, 4.6), P(13.4, 7.7));
                break;
            case LineIconKind.Copy:
                dc.DrawRoundedRectangle(null, pen, R(5.2, 4.8, 10.2, 10.2), X(1), Y(1));
                DrawPolyline(dc, pen, [(3.1, 12.5), (2.6, 12.5), (2.6, 2.7), (12.4, 2.7), (12.4, 3.2)]);
                break;
            case LineIconKind.Export:
                dc.DrawRoundedRectangle(null, pen, R(2.5, 9, 13, 6.4), X(1), Y(1));
                dc.DrawLine(pen, P(9, 11.7), P(9, 2.1));
                DrawPolyline(dc, pen, [(5.8, 5.4), (9, 2.1), (12.2, 5.4)]);
                break;
            case LineIconKind.Import:
                dc.DrawRoundedRectangle(null, pen, R(2.5, 9, 13, 6.4), X(1), Y(1));
                dc.DrawLine(pen, P(9, 2.1), P(9, 11.7));
                DrawPolyline(dc, pen, [(5.8, 8.4), (9, 11.7), (12.2, 8.4)]);
                break;
            case LineIconKind.Archive:
                dc.DrawRoundedRectangle(null, pen, R(2.2, 5, 13.6, 10.5), X(1), Y(1));
                dc.DrawRoundedRectangle(null, pen, R(1.5, 2.4, 15, 3.7), X(.8), Y(.8));
                dc.DrawLine(pen, P(6.6, 9), P(11.4, 9));
                break;
            case LineIconKind.Restore:
                DrawArc(dc, pen, P(9.4, 9.2), X(6), Y(6), -72, 285);
                DrawPolyline(dc, pen, [(2.1, 3.8), (2.4, 8), (6.4, 6.8)]);
                break;
            case LineIconKind.Plus:
                dc.DrawLine(pen, P(9, 3), P(9, 15));
                dc.DrawLine(pen, P(3, 9), P(15, 9));
                break;
            case LineIconKind.ArrowRight:
                dc.DrawLine(pen, P(2.5, 9), P(15.5, 9));
                DrawPolyline(dc, pen, [(10.7, 4.2), (15.5, 9), (10.7, 13.8)]);
                break;
            case LineIconKind.Processor:
                dc.DrawRoundedRectangle(null, pen, R(4.1, 4.1, 9.8, 9.8), X(1), Y(1));
                dc.DrawRoundedRectangle(null, pen, R(6.8, 6.8, 4.4, 4.4), X(.6), Y(.6));
                for (int i = 0; i < 4; i++)
                {
                    double p = 5.5 + (i * 2.35);
                    dc.DrawLine(pen, P(p, 1.5), P(p, 4.1));
                    dc.DrawLine(pen, P(p, 13.9), P(p, 16.5));
                    dc.DrawLine(pen, P(1.5, p), P(4.1, p));
                    dc.DrawLine(pen, P(13.9, p), P(16.5, p));
                }
                break;
            case LineIconKind.Pause:
                dc.DrawRoundedRectangle(Stroke, null, R(4.2, 3, 3.2, 12), X(.6), Y(.6));
                dc.DrawRoundedRectangle(Stroke, null, R(10.6, 3, 3.2, 12), X(.6), Y(.6));
                break;
            case LineIconKind.Stop:
                dc.DrawRoundedRectangle(Stroke, null, R(3.4, 3.4, 11.2, 11.2), X(1.2), Y(1.2));
                break;
            case LineIconKind.ExternalLink:
                dc.DrawRoundedRectangle(null, pen, R(2.4, 5.4, 10.2, 10.2), X(1), Y(1));
                DrawPolyline(dc, pen, [(9.2, 2.4), (15.6, 2.4), (15.6, 8.8)]);
                dc.DrawLine(pen, P(15.2, 2.8), P(8.2, 9.8));
                break;
        }
    }

    private void DrawSliders(DrawingContext dc, Pen pen)
    {
        foreach (var (y, knob) in new[] { (4.0, 6.0), (9.0, 12.0), (14.0, 8.0) })
        {
            dc.DrawLine(pen, P(2, y), P(16, y));
            dc.DrawEllipse(Stroke, new Pen((Brush)FindResource("PanelBrush"), StrokeThickness), P(knob, y), X(1.45), Y(1.45));
        }
    }

    private void DrawShield(DrawingContext dc, Pen pen)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(P(9, 1.4), false, true);
            context.LineTo(P(15, 3.7), true, false);
            context.LineTo(P(15, 8.5), true, false);
            context.BezierTo(P(15, 12.3), P(12.6, 15), P(9, 16.6), true, false);
            context.BezierTo(P(5.4, 15), P(3, 12.3), P(3, 8.5), true, false);
            context.LineTo(P(3, 3.7), true, false);
        }
        dc.DrawGeometry(null, pen, geometry);
    }

    private void DrawLink(DrawingContext dc, Pen pen)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(P(7.2, 11.1), false, false);
            context.LineTo(P(10.8, 7), true, false);
            context.BeginFigure(P(6.1, 13.4), false, false);
            context.LineTo(P(4.8, 14.7), true, false);
            context.BezierTo(P(2.8, 16.7), P(-.2, 13.7), P(1.8, 11.7), true, false);
            context.LineTo(P(4.2, 9.3), true, false);
            context.BezierTo(P(5.4, 8.1), P(7.2, 8.1), P(8.4, 9.3), true, false);
            context.BeginFigure(P(9.6, 8.7), false, false);
            context.BezierTo(P(10.8, 9.9), P(12.6, 9.9), P(13.8, 8.7), true, false);
            context.LineTo(P(16.2, 6.3), true, false);
            context.BezierTo(P(18.2, 4.3), P(15.2, 1.3), P(13.2, 3.3), true, false);
            context.LineTo(P(11.9, 4.6), true, false);
        }
        dc.DrawGeometry(null, pen, geometry);
    }

    private void DrawBalance(DrawingContext dc, Pen pen)
    {
        dc.DrawLine(pen, P(9, 2), P(9, 15.5));
        dc.DrawLine(pen, P(4, 5), P(14, 5));
        dc.DrawLine(pen, P(4, 5), P(1.5, 11));
        dc.DrawLine(pen, P(14, 5), P(16.5, 11));
        DrawPolyline(dc, pen, [(0.7, 11), (1.5, 13), (4, 13), (4.8, 11)]);
        DrawPolyline(dc, pen, [(13.2, 11), (14, 13), (16.5, 13), (17.3, 11)]);
        dc.DrawLine(pen, P(5.7, 16), P(12.3, 16));
    }

    private void DrawUserShoulders(DrawingContext dc, Pen pen)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(P(2.2, 16), false, false);
            context.BezierTo(P(2.6, 11.8), P(5.2, 10.2), P(9, 10.2), true, false);
            context.BezierTo(P(12.8, 10.2), P(15.4, 11.8), P(15.8, 16), true, false);
        }
        dc.DrawGeometry(null, pen, geometry);
    }

    private void DrawGear(DrawingContext dc, Pen pen)
    {
        dc.DrawEllipse(null, pen, P(9, 9), X(3), Y(3));
        dc.DrawEllipse(null, pen, P(9, 9), X(6.3), Y(6.3));
        for (int i = 0; i < 8; i++)
        {
            double angle = i * Math.PI / 4;
            dc.DrawLine(pen,
                P(9 + Math.Cos(angle) * 6.3, 9 + Math.Sin(angle) * 6.3),
                P(9 + Math.Cos(angle) * 8, 9 + Math.Sin(angle) * 8));
        }
    }

    private void DrawArc(DrawingContext dc, Pen pen, Point center, double radiusX, double radiusY, double startDegrees, double sweepDegrees)
    {
        double startRadians = startDegrees * Math.PI / 180.0;
        double endRadians = (startDegrees + sweepDegrees) * Math.PI / 180.0;
        var start = new Point(center.X + Math.Cos(startRadians) * radiusX, center.Y + Math.Sin(startRadians) * radiusY);
        var end = new Point(center.X + Math.Cos(endRadians) * radiusX, center.Y + Math.Sin(endRadians) * radiusY);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(start, false, false);
            context.ArcTo(end, new Size(radiusX, radiusY), 0, Math.Abs(sweepDegrees) > 180,
                sweepDegrees >= 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise, true, false);
        }
        dc.DrawGeometry(null, pen, geometry);
    }

    private void DrawPolyline(DrawingContext dc, Pen pen, (double X, double Y)[] points)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(P(points[0].X, points[0].Y), false, false);
            foreach (var point in points.Skip(1)) context.LineTo(P(point.X, point.Y), true, false);
        }
        dc.DrawGeometry(null, pen, geometry);
    }

    private void DrawPolygon(DrawingContext dc, Brush? fill, Pen? pen, (double X, double Y)[] points)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(P(points[0].X, points[0].Y), true, true);
            foreach (var point in points.Skip(1)) context.LineTo(P(point.X, point.Y), true, false);
        }
        dc.DrawGeometry(fill, pen, geometry);
    }

    private Point P(double x, double y) => new(X(x), Y(y));
    private Rect R(double x, double y, double width, double height) => new(X(x), Y(y), X(width), Y(height));
    private double X(double value) => value * ActualWidth / 18.0;
    private double Y(double value) => value * ActualHeight / 18.0;
}
