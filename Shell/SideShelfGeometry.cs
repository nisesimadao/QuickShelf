using System.Windows;
using System.Windows.Media;

namespace QuickShelf.Shell;

internal sealed class SideShelfGeometry
{
    public const double ExpandedTop = 24.0;
    public const double ExpandedCornerRadius = 32.0;
    public const double InverseFilletRadius = 24.0;
    public const double CollapsedWidth = 12.0;
    public const double CollapsedHeight = 260.0;

    private readonly PathGeometry _geometry = new();
    private readonly PathFigure _figure = new();

    public SideShelfGeometry()
    {
        _figure.IsClosed = true;
        _figure.IsFilled = true;
        _geometry.Figures.Add(_figure);
    }

    public Geometry Update(double hostWidth, double hostHeight, double panelWidth, double progress)
    {
        progress = Math.Clamp(progress, 0.0, 1.0);
        var grow = MotionProfile.Ease(progress);

        var width = Lerp(CollapsedWidth, panelWidth, grow);
        var expandedHeight = Math.Max(1.0, hostHeight - ExpandedTop);
        var height = Lerp(CollapsedHeight, expandedHeight, grow);
        var top = Lerp((hostHeight - CollapsedHeight) / 2.0, ExpandedTop, grow);
        var bottom = Math.Min(hostHeight, top + height);
        var right = hostWidth;
        var left = right - width;

        var corner = Math.Min(
            Lerp(CollapsedWidth, ExpandedCornerRadius, grow),
            Math.Min(width / 2.0, Math.Max(1.0, (bottom - top) / 2.0)));

        // The uploaded concept has a 24px inverse fillet at the physical
        // screen edge above the expanded panel. Keep it hidden while compact,
        // then grow it in near the end of the reveal.
        var fillet = InverseFilletRadius * MotionProfile.EaseRange(progress, 0.52, 1.0);
        fillet = Math.Min(fillet, top);

        _figure.Segments.Clear();
        _figure.StartPoint = new Point(right, top - fillet);

        // Screen-edge side.
        _figure.Segments.Add(new LineSegment(new Point(right, bottom), true));

        // Rounded lower-left corner, matching rounded-l-[32px].
        _figure.Segments.Add(new LineSegment(new Point(left + corner, bottom), true));
        _figure.Segments.Add(new QuadraticBezierSegment(
            new Point(left, bottom),
            new Point(left, bottom - corner),
            true));

        // Left edge and rounded upper-left corner.
        _figure.Segments.Add(new LineSegment(new Point(left, top + corner), true));
        _figure.Segments.Add(new QuadraticBezierSegment(
            new Point(left, top),
            new Point(left + corner, top),
            true));

        // Flat top of the panel body.
        _figure.Segments.Add(new LineSegment(new Point(right - fillet, top), true));

        if (fillet > 0.01)
        {
            // Exact cubic proportions used by the SVG inverse fillet in the ZIP:
            // M24,0 C24,13.255 13.255,24 0,24, traversed in reverse here.
            const double k = 0.4477083333333333;
            _figure.Segments.Add(new BezierSegment(
                new Point(right - fillet * k, top),
                new Point(right, top - fillet * k),
                new Point(right, top - fillet),
                true));
        }

        return _geometry;
    }

    private static double Lerp(double from, double to, double amount)
        => from + (to - from) * amount;
}

