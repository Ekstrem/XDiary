namespace GarminGateway.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

/// <summary>
/// BE-142 (один токен, владелец 2026-11-23 + @AR 21:32): пользователь вводит только ApiKey
/// (Personal API Key GCDC_…). OauthToken — короткоживущий серверный маркер, домен его
/// не требует; хранится опционально (до реального вызова Garmin API — заглушка).
/// </summary>
public interface ICredentials : IValueObject
{
    string ApiKey { get; }
    string? OauthToken { get; }
    string TokenExpiry { get; }
}
