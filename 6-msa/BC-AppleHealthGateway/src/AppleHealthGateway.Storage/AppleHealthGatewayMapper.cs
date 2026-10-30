namespace AppleHealthGateway.Storage;
using System.Text.Json;
using AppleHealthGateway.Domain.Abstraction;
using AppleHealthGateway.Domain.Implementation;
using AppleHealthGateway.InternalContracts;
public static class AppleHealthGatewayMapper
{
    private static List<string> ToList(string? json) => string.IsNullOrWhiteSpace(json) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
    private static string ToJson(IEnumerable<string> items) => JsonSerializer.Serialize(items.ToList());
    public static IAppleHealthGatewayAnemicModel MapToModel(AppleImportEntity e)
    {
        var m = new AnemicModel { Id = e.Id, Version = e.Version };
        m.Root = AppleImportRoot.CreateInstance(e.SourceId);
        m.ImportConfig = ImportConfig.CreateInstance(ToList(e.MetricsJson));
        m.LastImport = e.LastImport;
        m.ProcessedFiles = ToList(e.ProcessedFilesJson);
        return m;
    }
    public static void MapToEntity(IAppleHealthGatewayAnemicModel m, AppleImportEntity e)
    { e.Id = m.Id; e.Version = m.Version; e.SourceId = m.Root.SourceId; e.MetricsJson = ToJson(m.ImportConfig.Metrics); e.LastImport = m.LastImport; e.ProcessedFilesJson = ToJson(m.ProcessedFiles); }
    public static AppleImportReadModel MapToReadModel(AppleImportEntity e) => new AppleImportReadModel { Id = e.Id, SourceId = e.SourceId, Metrics = ToList(e.MetricsJson), LastImport = e.LastImport, ProcessedFiles = ToList(e.ProcessedFilesJson) };
}