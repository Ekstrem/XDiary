namespace Analytics.Domain.Models;

/// <summary>
/// Результат корреляции (Pearson) с пояснением и числом наблюдений.
/// </summary>
public class CorrelationResult
{
    public string Label { get; set; } = string.Empty;
    public double Coefficient { get; set; }
    public int N { get; set; }
    public string Explanation { get; set; } = string.Empty;
}
