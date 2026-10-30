namespace Portability.Storage;
public sealed class CsvDocumentEntity
{
    public Guid Id { get; set; }
    public string HeadersJson { get; set; } = "[]";
    public string RowsJson { get; set; } = "[]";
    public long Version { get; set; }
}