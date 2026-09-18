using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AudioTune.Controls;

public sealed class CorrectionBarChart : FrameworkElement
{
    private IReadOnlyList<(double Frequency, double Value)> _data = [];
    private int _hoverIndex = -1;

    public CorrectionBarChart()
    {
        ToolTipService.SetInitialShowDelay(this, 120);
        ToolTipService.SetBetweenShowDelay(this, 0);
        ToolTipService.SetShowDuration(this, 60000);
    }

    public void SetData(IReadOnlyList<(double Frequency, double Value)> data)
    {
        _data = data;
        _hoverIndex = -1;
        ToolTip = null;
        InvalidateVisual();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_data.Count == 0) return;

        const double left = 42, right = 8;
        var w = Math.Max(1, ActualWidth - left - right);
        var mouse = e.GetPosition(this);
        var best = -1;
        var bestDx = 12.0;
        for (int i = 0; i < _data.Count; i++)
        {
            var x = left + LogX(_data[i].Frequency) * w;
            var dx = Math.Abs(mouse.X - x);
            if (dx < bestDx)
            {
                bestDx = dx;
                best = i;
            }
        }

        if (best < 0)
        {
            ClearHover();
            return;
        }

        if (_hoverIndex != best)
        {
            _hoverIndex = best;
            var item = _data[best];
            var gain = item.Value > 0 ? $"+{item.Value:0.0}" : $"{item.Value:0.0}";
            ToolTip = $"Average L/R correction\n{item.Frequency:0.##} Hz\n{gain} dB";
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        ClearHover();
    }

    private void ClearHover()
    {
        if (_hoverIndex < 0) return;
        _hoverIndex = -1;
        ToolTip = null;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        const double left = 42, top = 10, bottom = 24, right = 8;
        var w = Math.Max(1, ActualWidth - left - right);
        var h = Math.Max(1, ActualHeight - top - bottom);
        var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(42, 59, 76)), 0.7);
        var label = new SolidColorBrush(Color.FromRgb(141, 160, 181));
        for (int i = 0; i <= 4; i++)
        {
            var y = top + i * h / 4;
            dc.DrawLine(gridPen, new Point(left, y), new Point(left + w, y));
        }
        var zero = top + h / 2;
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(120, 140, 160)), 1), new Point(left, zero), new Point(left + w, zero));
        if (_data.Count == 0) return;
        double scaleDb = Math.Min(12.0, Math.Max(6.0, Math.Ceiling(_data.Max(x => Math.Abs(x.Value)))));
        var barWidth = Math.Max(2, w / _data.Count * 0.45);
        var brush = new SolidColorBrush(Color.FromRgb(20, 140, 255));
        for (int i = 0; i < _data.Count; i++)
        {
            var item = _data[i];
            var x = left + LogX(item.Frequency) * w;
            var y = zero - item.Value / scaleDb * (h / 2);
            var rect = new Rect(x - barWidth / 2, Math.Min(zero, y), barWidth, Math.Max(1, Math.Abs(zero - y)));
            dc.DrawRectangle(brush, null, rect);
            if (i == _hoverIndex)
                dc.DrawRectangle(null, new Pen(Brushes.White, 1.2), new Rect(rect.X - 2, rect.Y - 2, rect.Width + 4, rect.Height + 4));
        }
        DrawText(dc, $"+{scaleDb:0}", label, 9, 4, top - 3);
        DrawText(dc, "0", label, 9, 15, zero - 7);
        DrawText(dc, $"-{scaleDb:0}", label, 9, 4, top + h - 9);
    }

    private static double LogX(double frequency)
    {
        var min = Math.Log10(30);
        var max = Math.Log10(18000);
        return (Math.Log10(Math.Clamp(frequency, 30, 18000)) - min) / (max - min);
    }

    private static void DrawText(DrawingContext dc, string text, Brush brush, double size, double x, double y)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);
        dc.DrawText(ft, new Point(x, y));
    }
}
