using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Globalization;

namespace SnmpVideoWall.Controls;

public sealed class TrafficGraph : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>> RxValuesProperty = AvaloniaProperty.Register<TrafficGraph, IReadOnlyList<double>>(nameof(RxValues), Array.Empty<double>());
    public static readonly StyledProperty<IReadOnlyList<double>> TxValuesProperty = AvaloniaProperty.Register<TrafficGraph, IReadOnlyList<double>>(nameof(TxValues), Array.Empty<double>());
    public static readonly StyledProperty<IReadOnlyList<DateTime>> TimesProperty = AvaloniaProperty.Register<TrafficGraph, IReadOnlyList<DateTime>>(nameof(Times), Array.Empty<DateTime>());
    public static readonly StyledProperty<int> WindowSecondsProperty = AvaloniaProperty.Register<TrafficGraph, int>(nameof(WindowSeconds), 60);
    public static readonly StyledProperty<double> RxLowProperty = AvaloniaProperty.Register<TrafficGraph, double>(nameof(RxLow), 0);
    public static readonly StyledProperty<double> RxHighProperty = AvaloniaProperty.Register<TrafficGraph, double>(nameof(RxHigh), 0);
    public static readonly StyledProperty<double> TxLowProperty = AvaloniaProperty.Register<TrafficGraph, double>(nameof(TxLow), 0);
    public static readonly StyledProperty<double> TxHighProperty = AvaloniaProperty.Register<TrafficGraph, double>(nameof(TxHigh), 0);

    public IReadOnlyList<double> RxValues { get => GetValue(RxValuesProperty); set => SetValue(RxValuesProperty, value); }
    public IReadOnlyList<double> TxValues { get => GetValue(TxValuesProperty); set => SetValue(TxValuesProperty, value); }
    public IReadOnlyList<DateTime> Times { get => GetValue(TimesProperty); set => SetValue(TimesProperty, value); }
    public int WindowSeconds { get => GetValue(WindowSecondsProperty); set => SetValue(WindowSecondsProperty, value); }
    public double RxLow { get => GetValue(RxLowProperty); set => SetValue(RxLowProperty, value); }
    public double RxHigh { get => GetValue(RxHighProperty); set => SetValue(RxHighProperty, value); }
    public double TxLow { get => GetValue(TxLowProperty); set => SetValue(TxLowProperty, value); }
    public double TxHigh { get => GetValue(TxHighProperty); set => SetValue(TxHighProperty, value); }

    public TrafficGraph() => ClipToBounds = true;

    public override void Render(DrawingContext c)
    {
        base.Render(c);
        var r = new Rect(0, 0, Bounds.Width, Bounds.Height);
        c.DrawRectangle(new SolidColorBrush(Color.Parse("#05080C")), new Pen(new SolidColorBrush(Color.Parse("#303946")), 1), r);
        if (r.Width < 100 || r.Height < 48) return;

        var now = DateTime.UtcNow;
        var start = now.AddSeconds(-Math.Max(60, WindowSeconds));
        var all = new List<(DateTime t, double rx, double tx)>();
        var n = Math.Min(Times.Count, Math.Min(RxValues.Count, TxValues.Count));
        for (var i = 0; i < n; i++)
            if (Times[i] >= start) all.Add((Times[i], Safe(RxValues[i]), Safe(TxValues[i])));
        if (all.Count == 0) return;

        var peak = Math.Max(Math.Max(all.Max(p => p.rx), all.Max(p => p.tx)), 1);
        var scaleMax = NiceCeiling(peak * 1.12);
        var compact = r.Width < 300 || r.Height < 100;
        var left = compact ? 43d : 62d; var right = 6d; var top = compact ? 7d : 10d; var bottom = compact ? 18d : 26d;
        var plot = new Rect(left, top, Math.Max(1, r.Width - left - right), Math.Max(1, r.Height - top - bottom));
        var grid = new Pen(new SolidColorBrush(Color.Parse("#465363")), 1);
        var textBrush = new SolidColorBrush(Color.Parse("#F1F5F9"));
        var typeface = new Typeface("Arial", FontStyle.Normal, FontWeight.Bold);
        for (var i = 0; i <= 4; i++)
        {
            var y = plot.Bottom - plot.Height * i / 4d;
            c.DrawLine(grid, new Point(plot.Left, y), new Point(plot.Right, y));
            var label = FormatRate(scaleMax * i / 4d);
            c.DrawText(new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 10, textBrush), new Point(1, y - 7));
        }
        for (var i = 0; i <= 4; i++)
        {
            var x = plot.Left + plot.Width * i / 4d;
            c.DrawLine(grid, new Point(x, plot.Top), new Point(x, plot.Bottom));
            var dt = start.AddSeconds(WindowSeconds * i / 4d).ToLocalTime();
            var label = WindowSeconds >= 3600 ? dt.ToString("HH:mm", CultureInfo.InvariantCulture) : dt.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            c.DrawText(new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 10, textBrush), new Point(Math.Clamp(x - 22, plot.Left, plot.Right - 48), plot.Bottom + 5));
        }

        DrawThreshold(c, plot, RxLow, scaleMax, new SolidColorBrush(Color.Parse("#4D9FE8")), true);
        DrawThreshold(c, plot, RxHigh, scaleMax, new SolidColorBrush(Color.Parse("#4D9FE8")), false);
        DrawThreshold(c, plot, TxLow, scaleMax, new SolidColorBrush(Color.Parse("#42D66B")), true);
        DrawThreshold(c, plot, TxHigh, scaleMax, new SolidColorBrush(Color.Parse("#42D66B")), false);

        var rxPoints = Points(all, plot, scaleMax, WindowSeconds, start, p => p.rx);
        var txPoints = Points(all, plot, scaleMax, WindowSeconds, start, p => p.tx);
        if (rxPoints.Count >= 1)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(rxPoints[0].X, plot.Bottom), true);
                ctx.LineTo(rxPoints[0]);
                foreach (var p in rxPoints.Skip(1)) ctx.LineTo(p);
                ctx.LineTo(new Point(rxPoints[^1].X, plot.Bottom));
                ctx.EndFigure(true);
            }
            c.DrawGeometry(new SolidColorBrush(Color.Parse("#4433BFFF")), null, geometry);
            DrawLine(c, rxPoints, new Pen(new SolidColorBrush(Color.Parse("#33BFFF")), 2.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round));
        }
        if (txPoints.Count >= 1) DrawLine(c, txPoints, new Pen(new SolidColorBrush(Color.Parse("#42E66C")), 2.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round));

        c.DrawText(new FormattedText("RX ▰", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, compact ? 9 : 11, new SolidColorBrush(Color.Parse("#33BFFF"))), new Point(plot.Left + 2, 0));
        c.DrawText(new FormattedText("TX ━", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, compact ? 9 : 11, new SolidColorBrush(Color.Parse("#42E66C"))), new Point(plot.Left + 45, 0));
    }

    private static List<Point> Points(List<(DateTime t, double rx, double tx)> data, Rect plot, double scale, int window, DateTime start, Func<(DateTime t, double rx, double tx), double> selector)
    {
        return data.Select(p => new Point(plot.Left + plot.Width * Math.Clamp((p.t - start).TotalSeconds / Math.Max(1, window), 0, 1), plot.Bottom - Math.Clamp(selector(p) / scale, 0, 1) * plot.Height)).ToList();
    }
    private static void DrawLine(DrawingContext c, List<Point> points, Pen pen)
    {
        if (points.Count == 1) { c.DrawEllipse(pen.Brush, null, points[0], 3, 3); return; }
        for (int i = 1; i < points.Count; i++) c.DrawLine(pen, points[i - 1], points[i]);
    }
    private static void DrawThreshold(DrawingContext c, Rect plot, double value, double max, IBrush brush, bool low)
    {
        if (value <= 0 || value >= max) return;
        var y = plot.Bottom - value / max * plot.Height;
        c.DrawLine(new Pen(brush, 1, dashStyle: new DashStyle(new double[] { 4, 4 }, 0)), new Point(plot.Left, y), new Point(plot.Right, y));
    }
    private static double Safe(double x) => double.IsFinite(x) ? Math.Max(0, x) : 0;
    private static double NiceCeiling(double x)
    {
        if (x <= 1) return 1;
        var p = Math.Pow(10, Math.Floor(Math.Log10(x)));
        var n = x / p;
        return (n <= 1 ? 1 : n <= 2 ? 2 : n <= 5 ? 5 : 10) * p;
    }
    private static string FormatRate(double bps)
    {
        if (bps >= 1_000_000_000) return $"{bps / 1_000_000_000:0.#} Gbps";
        if (bps >= 1_000_000) return $"{bps / 1_000_000:0.#} Mbps";
        if (bps >= 1_000) return $"{bps / 1_000:0.#} Kbps";
        return $"{bps:0} bps";
    }
}
