namespace Privacy.Domain.Specifications;
using System.Text.RegularExpressions;
internal sealed class PinFormatSpecification
{
    public bool IsSatisfiedBy(string pin) => Regex.IsMatch(pin, @"^\d{4,8}$");
    public string Reason => "PIN должен быть 4–8 цифр.";
}