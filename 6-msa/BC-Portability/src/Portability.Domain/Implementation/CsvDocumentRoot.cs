namespace Portability.Domain.Implementation;
using Portability.Domain.Abstraction;
public sealed class CsvDocumentRoot : ICsvDocumentRoot
{
    public IReadOnlyList<string> Headers { get; private set; } = new List<string>();
    private CsvDocumentRoot() { }
    public static ICsvDocumentRoot CreateInstance(IEnumerable<string> headers) => new CsvDocumentRoot { Headers = headers.ToList() };
}