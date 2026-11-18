namespace Portability.Domain.Specifications;
using Portability.Domain;
internal sealed class HeaderContractSpecification
{
    public bool IsSatisfiedBy(IReadOnlyList<string> headers) => headers.SequenceEqual(CsvHeaderContract.All);
    public string Reason => "Колонки CSV должны совпадать с CSV_HEADERS.";
}