namespace Catalog.Domain.Specifications;
using Catalog.Domain.Abstraction;
internal sealed class BrandToyOnlyValidator
{
    public bool IsSatisfiedBy(ICatalogAnemicModel model) => string.IsNullOrEmpty(model.Brand) || model.Root.Kind == "toy";
    public string Reason => "brand заполняется только для toy.";
}