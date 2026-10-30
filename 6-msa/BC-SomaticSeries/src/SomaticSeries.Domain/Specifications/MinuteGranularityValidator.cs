namespace SomaticSeries.Domain.Specifications;
using System.Text.RegularExpressions;
internal sealed class MinuteGranularityValidator
{
    public bool IsSatisfiedBy(string t) => Regex.IsMatch(t, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$");
    public string Reason => "t должен быть 'YYYY-MM-DDTHH:mm'.";
}