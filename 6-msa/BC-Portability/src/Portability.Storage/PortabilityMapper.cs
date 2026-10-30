namespace Portability.Storage;
using System.Text.Json;
using Portability.Domain.Abstraction;
using Portability.Domain.Implementation;
using Portability.InternalContracts;
public static class PortabilityMapper
{
    private static List<string> ToList(string? json) => string.IsNullOrWhiteSpace(json) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
    private static string ToJson(IEnumerable<string> items) => JsonSerializer.Serialize(items.ToList());
    public static ICsvDocumentAnemicModel MapToModel(CsvDocumentEntity e)
    { var m = new AnemicModel { Id = e.Id, Version = e.Version }; m.Root = CsvDocumentRoot.CreateInstance(ToList(e.HeadersJson)); m.Rows = ToList(e.RowsJson); return m; }
    public static void MapToEntity(ICsvDocumentAnemicModel m, CsvDocumentEntity e)
    { e.Id = m.Id; e.Version = m.Version; e.HeadersJson = ToJson(m.Root.Headers); e.RowsJson = ToJson(m.Rows); }
    public static CsvDocumentReadModel MapToReadModel(CsvDocumentEntity e) => new CsvDocumentReadModel { Id = e.Id, Headers = ToList(e.HeadersJson), Rows = ToList(e.RowsJson) };
}