namespace Portability.InternalContracts;
using XDiary.Contracts;
public sealed class CsvDocumentReadModel : IReadModel<Portability.Domain.IPortability>
{
    public Guid Id { get; set; }
    public IReadOnlyList<string> Headers { get; set; } = new List<string>();
    public IReadOnlyList<string> Rows { get; set; } = new List<string>();
}