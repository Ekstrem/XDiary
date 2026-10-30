namespace DailyMetrics.Domain.Specifications;
using System.Text.RegularExpressions;
internal sealed class DateFormatValidator
{
    public bool IsSatisfiedBy(string date) => Regex.IsMatch(date, @"^\d{4}-\d{2}-\d{2}$");
    public string Reason => "date должен быть 'YYYY-MM-DD'.";
}