using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using AppleHealthGateway.Domain;

namespace AppleHealthGateway.Domain.Tests;

/// <summary>
/// Строит анемичные модели AppleImport для тестов (аналог AppleImportDraft).
/// </summary>
public static class AppleImportBuilder
{
    public static Dictionary<string, IValueObject> BuildValueObjects(
        string sourceId = "apple-health",
        string[]? metrics = null,
        string lastImport = "2026-10-29T21:00",
        string[]? processedFiles = null)
    {
        return new Dictionary<string, IValueObject>
        {
            ["Root"] = new DynamicValueObject("AppleImportRoot", new Dictionary<string, object?>
            {
                ["SourceId"] = sourceId
            }),
            ["ImportConfig"] = new DynamicValueObject("ImportConfig", new Dictionary<string, object?>
            {
                ["Metrics"] = metrics ?? new[] { "hr", "hrv", "sleep", "restingHr" }
            }),
            ["LastImport"] = new DynamicValueObject("LastImport", new Dictionary<string, object?>
            {
                ["Timestamp"] = lastImport
            }),
            ["ProcessedFiles"] = new DynamicValueObject("ProcessedFiles", new Dictionary<string, object?>
            {
                ["Names"] = processedFiles ?? new[] { "export.xml" }
            })
        };
    }

    public static IAnemicModel<IAppleHealthGateway> NewImportModel(Guid id)
        => DynamicAnemicModel<IAppleHealthGateway>.Create(id, 0, Guid.NewGuid(), "UpsertImportState", "UpsertImportState", BuildValueObjects());

    public static IAnemicModel<IAppleHealthGateway> ExistingImportModel(Guid id)
        => DynamicAnemicModel<IAppleHealthGateway>.Create(id, 1, Guid.NewGuid(), "UpsertImportState", "UpsertImportState", BuildValueObjects());
}
