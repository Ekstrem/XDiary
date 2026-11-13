namespace Client.Domain.Specifications;

using System.Text.RegularExpressions;

/// <summary>
/// TE-097 (канон `AR-025`, 2026-11-13): нормализация номера и проверка формата.
///
/// Почему нормализация обязательна: инвариант «`phone` уникален» обходится форматированием —
/// `+7 900 000-00-00` и `+7900000000` как строки разные, но это один клиент. Поэтому
/// нормализуем ДО валидации, лимитера, проверки уникальности и отправки OTP.
/// Правило (канон): убрать всё кроме цифр и ведущего `+`; `8XXXXXXXXXX` → `+7XXXXXXXXXX`;
/// валидность — E.164 `^\+[1-9][0-9]{6,14}$`.
/// </summary>
public static class PhoneFormatValidator
{
    private static readonly Regex E164 = new(@"^\+[1-9][0-9]{6,14}$", RegexOptions.Compiled);

    /// <summary>Нормализованный номер (`+<цифры>`) или <c>null</c>, если цифр нет.</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return null;
        // канон: 11 цифр с ведущей `8` — это российский формат без `+`
        if (digits.Length == 11 && digits[0] == '8') digits = "7" + digits[1..];
        return "+" + digits;
    }

    /// <summary>Валиден ли УЖЕ нормализованный номер (E.164).</summary>
    public static bool IsValid(string? normalized)
    {
        if (normalized is null) return false;
        if (!E164.IsMatch(normalized)) return false;
        // AR-025 (@AR 23:53): российский мобильный — +7 + 10 цифр (всего 11);
        // +7 + 9 цифр (всего 10) — несуществующий номер, режется здесь (не в Normalize).
        if (normalized.StartsWith("+7") && normalized.Length != 12) return false;
        return true;
    }
}
