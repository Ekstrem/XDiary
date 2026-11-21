namespace GarminGateway.Api.Contracts;

using System.Globalization;
using GarminGateway.Domain.Abstraction;
using GarminGateway.InternalContracts;

/// <summary>BE-142: единый публичный ответ Connect/Sync/FetchActivities/State. Секреты сюда не попадают.</summary>
public sealed record GarminConnectionState
{
    public bool Connected { get; init; }
    public string SourceId { get; init; } = "garmin";
    public string? TokenExpiry { get; init; }
    public IReadOnlyList<string> Metrics { get; init; } = Array.Empty<string>();
    public int WindowDays { get; init; }
    public string? LastSync { get; init; }
    public bool RequiresReauth { get; init; }

    public static GarminConnectionState FromAnemicModel(IGarminGatewayAnemicModel model)
    {
        var credentials = model.Credentials;
        return new GarminConnectionState
        {
            Connected = HasCredentials(credentials.ApiKey, credentials.OauthToken),
            SourceId = model.Root?.SourceId ?? "garmin",
            TokenExpiry = NullIfEmpty(credentials.TokenExpiry),
            Metrics = model.SyncConfig?.Metrics ?? Array.Empty<string>(),
            WindowDays = model.SyncConfig?.WindowDays ?? 0,
            LastSync = NullIfEmpty(model.LastSync),
            RequiresReauth = IsExpired(credentials.TokenExpiry),
        };
    }

    public static GarminConnectionState FromReadModel(GarminSyncReadModel model)
        => new()
        {
            Connected = HasCredentials(model.ApiKey, model.OauthToken),
            SourceId = model.SourceId,
            TokenExpiry = NullIfEmpty(model.TokenExpiry),
            Metrics = model.Metrics,
            WindowDays = model.WindowDays,
            LastSync = NullIfEmpty(model.LastSync),
            RequiresReauth = IsExpired(model.TokenExpiry),
        };

    private static bool HasCredentials(string apiKey, string oauthToken)
        => !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(oauthToken);

    private static string? NullIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool IsExpired(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expiry)) return true;
        return expiry <= DateTimeOffset.UtcNow;
    }
}

/// <summary>Машиночитаемая ошибка без внутренних ответов Garmin, stack trace и секретов.</summary>
public sealed record GarminApiError(string Code, string Message);
