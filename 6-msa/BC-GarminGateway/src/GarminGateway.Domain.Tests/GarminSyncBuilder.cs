using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using GarminGateway.Domain;

namespace GarminGateway.Domain.Tests;

/// <summary>
/// Строит анемичные модели GarminSync для тестов (аналог GarminSyncDraft).
/// </summary>
public static class GarminSyncBuilder
{
    public static Dictionary<string, IValueObject> BuildValueObjects(
        string sourceId = "garmin",
        string apiKey = "GCDC_key",
        string oauthToken = "token",
        string tokenExpiry = "2026-10-30T00:00",
        string[]? metrics = null,
        int windowDays = 30,
        string lastSync = "2026-10-29T21:00")
    {
        return new Dictionary<string, IValueObject>
        {
            ["Root"] = new DynamicValueObject("GarminSyncRoot", new Dictionary<string, object?>
            {
                ["SourceId"] = sourceId
            }),
            ["Credentials"] = new DynamicValueObject("Credentials", new Dictionary<string, object?>
            {
                ["ApiKey"] = apiKey,
                ["OauthToken"] = oauthToken,
                ["TokenExpiry"] = tokenExpiry
            }),
            ["SyncConfig"] = new DynamicValueObject("SyncConfig", new Dictionary<string, object?>
            {
                ["Metrics"] = metrics ?? new[] { "stress", "sleep", "hrv" },
                ["WindowDays"] = windowDays
            }),
            ["LastSync"] = new DynamicValueObject("LastSync", new Dictionary<string, object?>
            {
                ["Timestamp"] = lastSync
            })
        };
    }

    public static IAnemicModel<IGarminGateway> NewSyncModel(Guid id)
        => DynamicAnemicModel<IGarminGateway>.Create(id, 0, Guid.NewGuid(), "UpsertSyncState", "UpsertSyncState", BuildValueObjects());

    public static IAnemicModel<IGarminGateway> ExistingSyncModel(Guid id)
        => DynamicAnemicModel<IGarminGateway>.Create(id, 1, Guid.NewGuid(), "UpsertSyncState", "UpsertSyncState", BuildValueObjects());
}
