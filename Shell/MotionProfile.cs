namespace QuickShelf.Shell;

internal static class MotionProfile
{
    public static double Ease(double progress)
    {
        var t = Math.Clamp(progress, 0.0, 1.0);
        return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
    }

    public static double Range(double value, double start, double end)
    {
        if (end <= start)
        {
            return value >= end ? 1.0 : 0.0;
        }

        return Math.Clamp((value - start) / (end - start), 0.0, 1.0);
    }

    public static double EaseRange(double value, double start, double end)
        => Ease(Range(value, start, end));

    public static int ScaleDuration(int baseDurationMs, double distance)
    {
        var factor = Math.Clamp(Math.Abs(distance), 0.0, 1.0);
        return Math.Max(105, (int)Math.Round(baseDurationMs * (0.72 + 0.28 * Math.Sqrt(factor))));
    }
}
