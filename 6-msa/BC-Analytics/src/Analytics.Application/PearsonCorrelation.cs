namespace Analytics.Application;

/// <summary>
/// Коэффициент корреляции Пирсона.
/// </summary>
public static class PearsonCorrelation
{
    public static double Compute(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
    {
        var n = xs.Count;
        if (n != ys.Count || n < 2)
            return 0;

        var sumX = 0.0;
        var sumY = 0.0;
        var sumXY = 0.0;
        var sumX2 = 0.0;
        var sumY2 = 0.0;

        for (var i = 0; i < n; i++)
        {
            sumX += xs[i];
            sumY += ys[i];
            sumXY += xs[i] * ys[i];
            sumX2 += xs[i] * xs[i];
            sumY2 += ys[i] * ys[i];
        }

        var numerator = n * sumXY - sumX * sumY;
        var denominator = Math.Sqrt((n * sumX2 - sumX * sumX) * (n * sumY2 - sumY * sumY));
        return denominator == 0 ? 0 : numerator / denominator;
    }
}
