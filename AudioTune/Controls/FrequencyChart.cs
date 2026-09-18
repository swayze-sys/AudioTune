using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AudioTune.Controls;

public sealed class FrequencyChart : FrameworkElement
{
    public sealed record Series(string Name, IReadOnlyList<(double Frequency, double Value)> Points, Brush Brush, bool Dashed = false);

    public sealed class PointEventArgs : EventArgs
    {
        public PointEventArgs(string seriesName, double frequency, double value, string unit)
        {
            SeriesName = seriesName;
            Frequency = frequency;
            Value = value;
            Unit = unit;
        }

        public string SeriesName { get; }
        public double Frequency { get; }
        public double Value { get; }
        public string Unit { get; }
    }

    private IReadOnlyList<Series> _series = [];
    private int _hoverSeries = -1;
    private int _hoverPoint = -1;
    private int _selectedSeries = -1;
    private int _selectedPoint = -1;

    public double MinimumY { get; set; } = -80;
    public double MaximumY { get; set; } = -10;
    public string YUnit { get; set; } = "dBFS";
    public bool ShowZeroLine { get; set; }

    /// <summary>Raised whenever the pointer moves onto a data point.</summary>
    public event EventHandler<PointEventArgs>? PointInspected;

    /// <summary>Raised when a data point is clicked. The selected point stays highlighted.</summary>
    public event EventHandler<PointEventArgs>? PointSelected;

    public FrequencyChart()
    {
        SnapsToDevicePixels = true;
        Cursor = Cursors.Cross;
        ToolTipService.SetInitialShowDelay(this, 120);
        ToolTipService.SetBetweenShowDelay(this, 0);
        ToolTipService.SetShowDuration(this, 60000);
    }

    public void SetSeries(params Series[] series)
    {
        _series = series;
        _hoverSeries = -1;
        _hoverPoint = -1;
        _selectedSeries = -1;
        _selectedPoint = -1;
        ToolTip = null;
        InvalidateVisual();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var nearest = FindNearestPoint(e.GetPosition(this), 13.0);
        if (nearest.Series < 0)
        {
            ClearHover();
            return;
        }

        if (_hoverSeries != nearest.Series || _hoverPoint != nearest.Point)
        {
            _hoverSeries = nearest.Series;
            _hoverPoint = nearest.Point;
            var s = _series[_hoverSeries];
            var p = s.Points[_hoverPoint];
            ToolTip = $"{s.Name}\n{FormatFrequencyPrecise(p.Frequency)}\n{FormatValue(p.Value)} {YUnit}";
            PointInspected?.Invoke(this, new PointEventArgs(s.Name, p.Frequency, p.Value, YUnit));
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var nearest = FindNearestPoint(e.GetPosition(this), 16.0);
        if (nearest.Series < 0) return;

        _selectedSeries = nearest.Series;
        _selectedPoint = nearest.Point;
        var s = _series[_selectedSeries];
        var p = s.Points[_selectedPoint];
        var args = new PointEventArgs(s.Name, p.Frequency, p.Value, YUnit);
        PointSelected?.Invoke(this, args);
        PointInspected?.Invoke(this, args);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        ClearHover();
    }

    private (int Series, int Point) FindNearestPoint(Point mouse, double maxDistance)
    {
        if (ActualWidth < 100 || ActualHeight < 80 || _series.Count == 0) return (-1, -1);

        GetPlotArea(out var left, out var top, out var w, out var h);
        if (mouse.X < left - maxDistance || mouse.X > left + w + maxDistance ||
            mouse.Y < top - maxDistance || mouse.Y > top + h + maxDistance)
            return (-1, -1);

        double bestDistance = maxDistance;
        int bestSeries = -1;
        int bestPoint = -1;

        for (int si = 0; si < _series.Count; si++)
        {
            var s = _series[si];
            for (int pi = 0; pi < s.Points.Count; pi++)
            {
                var p = s.Points[pi];
                var px = left + LogX(p.Frequency) * w;
                var py = MapY(p.Value, top, h);
                var dx = mouse.X - px;
                var dy = mouse.Y - py;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestSeries = si;
                    bestPoint = pi;
                }
            }
        }

        return (bestSeries, bestPoint);
    }

    private void ClearHover()
    {
        if (_hoverSeries == -1 && _hoverPoint == -1) return;
        _hoverSeries = -1;
        _hoverPoint = -1;
        ToolTip = null;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (ActualWidth < 100 || ActualHeight < 80) return;

        GetPlotArea(out var left, out var top, out var w, out var h);
        var grid = new Pen(new SolidColorBrush(Color.FromRgb(42, 59, 76)), 0.7);
        grid.Freeze();
        var textBrush = new SolidColorBrush(Color.FromRgb(141, 160, 181));
        textBrush.Freeze();

        double[] freqTicks = [30, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 18000];
        foreach (var f in freqTicks)
        {
            var x = left + LogX(f) * w;
            dc.DrawLine(grid, new Point(x, top), new Point(x, top + h));
            DrawText(dc, FormatFreq(f), textBrush, 10, x - 12, top + h + 8);
        }

        const int yLines = 5;
        for (int i = 0; i <= yLines; i++)
        {
            var t = i / (double)yLines;
            var y = top + t * h;
            var val = MaximumY - t * (MaximumY - MinimumY);
            dc.DrawLine(grid, new Point(left, y), new Point(left + w, y));
            DrawText(dc, $"{val:0}", textBrush, 10, 3, y - 7);
        }

        if (ShowZeroLine && IsBetweenZero())
        {
            var y = MapY(0, top, h);
            var zero = new Pen(new SolidColorBrush(Color.FromRgb(115, 133, 150)), 1) { DashStyle = DashStyles.Dash };
            dc.DrawLine(zero, new Point(left, y), new Point(left + w, y));
        }

        dc.PushClip(new RectangleGeometry(new Rect(left, top, w, h)));
        for (int si = 0; si < _series.Count; si++)
        {
            var s = _series[si];
            if (s.Points.Count < 1) continue;

            if (s.Points.Count >= 2)
            {
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    var first = s.Points[0];
                    ctx.BeginFigure(new Point(left + LogX(first.Frequency) * w, MapY(first.Value, top, h)), false, false);
                    foreach (var p in s.Points.Skip(1))
                        ctx.LineTo(new Point(left + LogX(p.Frequency) * w, MapY(p.Value, top, h)), true, false);
                }
                geometry.Freeze();
                var pen = new Pen(s.Brush, 2.2) { DashStyle = s.Dashed ? DashStyles.Dash : DashStyles.Solid };
                pen.Freeze();
                dc.DrawGeometry(null, pen, geometry);
            }

            for (int pi = 0; pi < s.Points.Count; pi++)
            {
                var p = s.Points[pi];
                var center = new Point(left + LogX(p.Frequency) * w, MapY(p.Value, top, h));
                var isHovered = si == _hoverSeries && pi == _hoverPoint;
                var isSelected = si == _selectedSeries && pi == _selectedPoint;

                if (isSelected)
                {
                    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), new Pen(s.Brush, 1.8), center, 8.5, 8.5);
                    dc.DrawEllipse(s.Dashed ? Brushes.Transparent : s.Brush, new Pen(Brushes.White, 1.4), center, 4.8, 4.8);
                }
                else if (isHovered)
                {
                    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(42, 255, 255, 255)), null, center, 7.0, 7.0);
                    dc.DrawEllipse(s.Dashed ? Brushes.Transparent : s.Brush, new Pen(Brushes.White, 1.0), center, 4.2, 4.2);
                }
                else if (s.Dashed)
                {
                    dc.DrawEllipse(Brushes.Transparent, new Pen(s.Brush, 1.0), center, 2.6, 2.6);
                }
                else
                {
                    dc.DrawEllipse(s.Brush, null, center, 2.8, 2.8);
                }
            }
        }
        dc.Pop();
        DrawText(dc, YUnit, textBrush, 10, 3, 2);
    }

    private void GetPlotArea(out double left, out double top, out double w, out double h)
    {
        const double right = 12, bottom = 30;
        left = 48;
        top = 14;
        w = Math.Max(1, ActualWidth - left - right);
        h = Math.Max(1, ActualHeight - top - bottom);
    }

    private bool IsBetweenZero()
        => (MinimumY < 0 && MaximumY > 0) || (MaximumY < 0 && MinimumY > 0);

    private double MapY(double value, double top, double h)
        => top + (MaximumY - value) / (MaximumY - MinimumY) * h;

    private static double LogX(double frequency)
    {
        var min = Math.Log10(30);
        var max = Math.Log10(18000);
        return (Math.Log10(Math.Clamp(frequency, 30, 18000)) - min) / (max - min);
    }

    private static string FormatFreq(double f) => f >= 1000 ? $"{f / 1000:0.#}k" : $"{f:0}";

    public static string FormatFrequencyPrecise(double f)
        => f >= 1000 ? $"{f / 1000:0.###} kHz ({f:0} Hz)" : $"{f:0} Hz";

    public static string FormatValue(double value)
        => value > 0 ? $"+{value:0.0}" : $"{value:0.0}";

    private static void DrawText(DrawingContext dc, string text, Brush brush, double size, double x, double y)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);
        dc.DrawText(ft, new Point(x, y));
    }
}
