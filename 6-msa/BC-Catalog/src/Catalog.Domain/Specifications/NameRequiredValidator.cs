namespace Catalog.Domain.Specifications;
using Catalog.Domain.Abstraction;
internal sealed class NameRequiredValidator
{
    public bool IsSatisfiedBy(ICatalogAnemicModel model) => !string.IsNullOrWhiteSpace(model.Root.Name);
    public string Reason => "Имя элемента обязательно.";
}