using Analytics.Application;
using Xunit;

namespace Analytics.Domain.Tests;

/// <summary>
/// Тесты коэффициента корреляции Пирсона.
/// </summary>
public class PearsonCorrelationTest
{
    [Fact]
    public void PerfectPositive_CorrelationIsOne()
    {
        var r = PearsonCorrelation.Compute(new double[] { 1, 2, 3 }, new double[] { 1, 2, 3 });
        Assert.Equal(1.0, r, 3);
    }

    [Fact]
    public void PerfectNegative_CorrelationIsMinusOne()
    {
        var r = PearsonCorrelation.Compute(new double[] { 1, 2, 3 }, new double[] { 3, 2, 1 });
        Assert.Equal(-1.0, r, 3);
    }

    [Fact]
    public void InsufficientSamples_ReturnsZero()
    {
        var r = PearsonCorrelation.Compute(new double[] { 1 }, new double[] { 1 });
        Assert.Equal(0.0, r, 3);
    }

    [Fact]
    public void Coefficient_IsWithinRange()
    {
        var xs = new double[] { 10, 20, 30, 40, 50 };
        var ys = new double[] { 5, 18, 27, 44, 60 };
        var r = PearsonCorrelation.Compute(xs, ys);
        Assert.InRange(r, -1.0, 1.0);
    }
}
