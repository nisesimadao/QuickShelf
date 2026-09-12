using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace QuickShelf.Shell;

internal sealed class WindowRegionService
{
    private const int Alternate = 1;
    private const int RgnOr = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreatePolygonRgn([In] NativePoint[] points, int count, int fillMode);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

    [DllImport("gdi32.dll")]
    private static extern int CombineRgn(IntPtr dest, IntPtr src1, IntPtr src2, int mode);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    public bool Apply(Window window, Geometry geometry)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        if (geometry == Geometry.Empty || geometry.Bounds.IsEmpty)
        {
            var empty = CreateRectRgn(0, 0, 0, 0);
            if (empty == IntPtr.Zero)
            {
                return false;
            }

            var result = SetWindowRgn(hwnd, empty, true);
            if (result == 0)
            {
                _ = DeleteObject(empty);
                return false;
            }

            return true;
        }

        var source = PresentationSource.FromVisual(window);
        var transform = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var flattened = geometry.GetFlattenedPathGeometry(0.7, ToleranceType.Absolute);
        var combined = CreateRectRgn(0, 0, 0, 0);
        if (combined == IntPtr.Zero)
        {
            return false;
        }

        var hasFigure = false;
        try
        {
            foreach (var figure in flattened.Figures)
            {
                var points = FlattenFigure(figure)
                    .Select(transform.Transform)
                    .Select(p => new NativePoint
                    {
                        X = (int)Math.Round(p.X),
                        Y = (int)Math.Round(p.Y)
                    })
                    .ToArray();

                if (points.Length < 3)
                {
                    continue;
                }

                var piece = CreatePolygonRgn(points, points.Length, Alternate);
                if (piece == IntPtr.Zero)
                {
                    continue;
                }

                try
                {
                    _ = CombineRgn(combined, combined, piece, RgnOr);
                    hasFigure = true;
                }
                finally
                {
                    _ = DeleteObject(piece);
                }
            }

            if (!hasFigure)
            {
                return false;
            }

            var setResult = SetWindowRgn(hwnd, combined, true);
            if (setResult != 0)
            {
                combined = IntPtr.Zero;
                return true;
            }

            return false;
        }
        finally
        {
            if (combined != IntPtr.Zero)
            {
                _ = DeleteObject(combined);
            }
        }
    }

    public void Reset(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            _ = SetWindowRgn(hwnd, IntPtr.Zero, true);
        }
    }

    private static IEnumerable<Point> FlattenFigure(PathFigure figure)
    {
        yield return figure.StartPoint;
        foreach (var segment in figure.Segments)
        {
            switch (segment)
            {
                case LineSegment line:
                    yield return line.Point;
                    break;
                case PolyLineSegment poly:
                    foreach (var p in poly.Points)
                    {
                        yield return p;
                    }
                    break;
            }
        }
    }
}

