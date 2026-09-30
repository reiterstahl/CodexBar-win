using System.Windows;
using System.Windows.Media;

namespace CodexBar.Windows.Tray;

public enum MeterKind
{
    Bar,
    Ring,
    Gauge,
    Blocks,
}

/// <summary>
/// Lightweight quota visualization drawn directly in <see cref="OnRender"/>: a linear bar
/// with an optional pace marker, segmented blocks, a full ring, or a half-circle gauge.
/// </summary>
public sealed class UsageMeter : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind),
        typeof(MeterKind),
        typeof(UsageMeter),
        new FrameworkPropertyMetadata(MeterKind.Bar, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(UsageMeter),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MarkerProperty = DependencyProperty.Register(
        nameof(Marker),
        typeof(double),
        typeof(UsageMeter),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness),
        typeof(double),
        typeof(UsageMeter),
        new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
        nameof(Segments),
        typeof(int),
        typeof(UsageMeter),
        new FrameworkPropertyMetadata(20, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillBrushProperty = DependencyProperty.Register(
        nameof(FillBrush),
        typeof(System.Windows.Media.Brush),
        typeof(UsageMeter),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush),
        typeof(System.Windows.Media.Brush),
        typeof(UsageMeter),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MarkerBrushProperty = DependencyProperty.Register(
        nameof(MarkerBrush),
        typeof(System.Windows.Media.Brush),
        typeof(UsageMeter),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public MeterKind Kind
    {
        get => (MeterKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>Filled share, from 0 to 100.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Pace marker position from 0 to 100, or NaN to hide it. Bars only.</summary>
    public double Marker
    {
        get => (double)GetValue(MarkerProperty);
        set => SetValue(MarkerProperty, value);
    }

    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public int Segments
    {
        get => (int)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public System.Windows.Media.Brush? FillBrush
    {
        get => (System.Windows.Media.Brush?)GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    public System.Windows.Media.Brush? TrackBrush
    {
        get => (System.Windows.Media.Brush?)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public System.Windows.Media.Brush? MarkerBrush
    {
        get => (System.Windows.Media.Brush?)GetValue(MarkerBrushProperty);
        set => SetValue(MarkerBrushProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        double fraction = Math.Clamp(double.IsFinite(Value) ? Value : 0, 0, 100) / 100;
        switch (Kind)
        {
            case MeterKind.Ring:
                RenderRing(drawingContext, width, height, fraction);
                break;
            case MeterKind.Gauge:
                RenderGauge(drawingContext, width, height, fraction);
                break;
            case MeterKind.Blocks:
                RenderBlocks(drawingContext, width, height, fraction);
                break;
            default:
                RenderBar(drawingContext, width, height, fraction);
                break;
        }
    }

    private void RenderBar(DrawingContext context, double width, double height, double fraction)
    {
        double thickness = Math.Min(Thickness, height);
        double top = (height - thickness) / 2;
        double radius = thickness / 2;
        context.DrawRoundedRectangle(TrackBrush, null, new Rect(0, top, width, thickness), radius, radius);

        double fillWidth = width * fraction;
        if (fillWidth > 0.5)
        {
            double fillRadius = Math.Min(radius, fillWidth / 2);
            context.DrawRoundedRectangle(
                FillBrush,
                null,
                new Rect(0, top, fillWidth, thickness),
                fillRadius,
                fillRadius);
        }

        if (double.IsFinite(Marker) && MarkerBrush is not null)
        {
            double x = Math.Clamp(width * Math.Clamp(Marker, 0, 100) / 100, 1, width - 1);
            context.PushOpacity(0.75);
            context.DrawRoundedRectangle(MarkerBrush, null, new Rect(x - 1, 0, 2, height), 1, 1);
            context.Pop();
        }
    }

    private void RenderBlocks(DrawingContext context, double width, double height, double fraction)
    {
        int segments = Math.Max(1, Segments);
        const double gap = 3;
        double blockWidth = Math.Max(1, (width - (gap * (segments - 1))) / segments);
        double thickness = Math.Min(Thickness + 6, height);
        double top = (height - thickness) / 2;
        int filled = (int)Math.Round(fraction * segments);
        for (int index = 0; index < segments; index++)
        {
            double left = index * (blockWidth + gap);
            context.DrawRoundedRectangle(
                index < filled ? FillBrush : TrackBrush,
                null,
                new Rect(left, top, blockWidth, thickness),
                2.5,
                2.5);
        }
    }

    private void RenderRing(DrawingContext context, double width, double height, double fraction)
    {
        double thickness = Thickness;
        double size = Math.Min(width, height);
        double radius = (size - thickness) / 2;
        if (radius <= 0)
        {
            return;
        }

        var center = new System.Windows.Point(width / 2, height / 2);
        context.DrawEllipse(null, new System.Windows.Media.Pen(TrackBrush, thickness), center, radius, radius);
        if (fraction <= 0)
        {
            return;
        }

        var pen = new System.Windows.Media.Pen(FillBrush, thickness);
        if (fraction >= 0.999)
        {
            context.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        double sweep = 360 * fraction;
        System.Windows.Point start = PointOnRing(center, radius, 0);
        System.Windows.Point end = PointOnRing(center, radius, sweep);
        context.DrawGeometry(null, pen, Arc(start, end, radius, sweep > 180));
    }

    private void RenderGauge(DrawingContext context, double width, double height, double fraction)
    {
        double thickness = Thickness;
        double radius = Math.Min(width / 2, height - (thickness / 2)) - (thickness / 2);
        if (radius <= 0)
        {
            return;
        }

        var center = new System.Windows.Point(width / 2, (thickness / 2) + radius);
        System.Windows.Point left = PointOnGauge(center, radius, 0);
        System.Windows.Point right = PointOnGauge(center, radius, 180);
        context.DrawGeometry(
            null,
            new System.Windows.Media.Pen(TrackBrush, thickness),
            Arc(left, right, radius, isLargeArc: false));
        if (fraction <= 0)
        {
            return;
        }

        System.Windows.Point end = PointOnGauge(center, radius, 180 * Math.Min(fraction, 0.9999));
        context.DrawGeometry(
            null,
            new System.Windows.Media.Pen(FillBrush, thickness),
            Arc(left, end, radius, isLargeArc: false));
    }

    // Degrees are measured clockwise from 12 o'clock.
    private static System.Windows.Point PointOnRing(System.Windows.Point center, double radius, double degrees)
    {
        double radians = degrees * Math.PI / 180;
        return new System.Windows.Point(
            center.X + (radius * Math.Sin(radians)),
            center.Y - (radius * Math.Cos(radians)));
    }

    // Degrees are measured clockwise from 9 o'clock, over the top.
    private static System.Windows.Point PointOnGauge(System.Windows.Point center, double radius, double degrees)
    {
        double radians = degrees * Math.PI / 180;
        return new System.Windows.Point(
            center.X - (radius * Math.Cos(radians)),
            center.Y - (radius * Math.Sin(radians)));
    }

    private static StreamGeometry Arc(
        System.Windows.Point start,
        System.Windows.Point end,
        double radius,
        bool isLargeArc)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(start, isFilled: false, isClosed: false);
            context.ArcTo(
                end,
                new System.Windows.Size(radius, radius),
                0,
                isLargeArc,
                SweepDirection.Clockwise,
                isStroked: true,
                isSmoothJoin: false);
        }
        geometry.Freeze();
        return geometry;
    }
}
