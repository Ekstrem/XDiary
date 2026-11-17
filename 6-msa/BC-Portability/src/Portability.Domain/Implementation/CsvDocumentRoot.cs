namespace Portability.Domain.Implementation;
using Portability.Domain.Abstraction;

// record (positional) — getter-only, мутация через `with`/создание новой версии.
public sealed record CsvDocumentRoot(IReadOnlyList<string> Headers) : ICsvDocumentRoot
{
    public static ICsvDocumentRoot CreateInstance(IEnumerable<string> headers) => new CsvDocumentRoot(headers.ToList());
}
