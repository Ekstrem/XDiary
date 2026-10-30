namespace Catalog.Domain.Specifications;
using Catalog.Domain.Abstraction;
internal sealed class ArchiveToyOnlyValidator
{
    public bool IsSatisfiedBy(ICatalogAnemicModel model) => model.Root.Kind == "toy";
    public string Reason => "Archive/Restore применяются только для toy.";
}