using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Portability.Application.Csv;
using Portability.Domain;

namespace Portability.Domain.Tests;

/// <summary>
/// Строит анемичные модели CsvDocument для тестов (аналог CsvDocumentDraft).
/// </summary>
public static class CsvDocumentBuilder
{
    public static Dictionary<string, IValueObject> BuildValueObjects(
        string sourceId = "records",
        string[]? headers = null,
        string[]? rows = null)
    {
        return new Dictionary<string, IValueObject>
        {
            ["Root"] = new DynamicValueObject("CsvDocumentRoot", new Dictionary<string, object?>
            {
                ["SourceId"] = sourceId
            }),
            ["Headers"] = new DynamicValueObject("CsvHeaders", new Dictionary<string, object?>
            {
                ["Headers"] = headers ?? CsvHeaders.Record
            }),
            ["Rows"] = new DynamicValueObject("CsvRows", new Dictionary<string, object?>
            {
                ["Rows"] = rows ?? new[] { "1,2026-10-29T21:00,30" }
            })
        };
    }

    public static IAnemicModel<IPortability> NewDocumentModel(Guid id)
        => DynamicAnemicModel<IPortability>.Create(id, 0, Guid.NewGuid(), "UpsertDocument", "UpsertDocument", BuildValueObjects());

    public static IAnemicModel<IPortability> ExistingDocumentModel(Guid id)
        => DynamicAnemicModel<IPortability>.Create(id, 1, Guid.NewGuid(), "UpsertDocument", "UpsertDocument", BuildValueObjects());
}
