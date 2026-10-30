namespace Privacy.Domain.Specifications;
using System.Text.RegularExpressions;
public sealed class PinFormatValidator
{
    public bool IsSatisfiedBy(string pin) => Regex.IsMatch(pin, @"^\d{4,8}$");
    public string Reason => "PIN должен быть 4–8 цифр.";
}