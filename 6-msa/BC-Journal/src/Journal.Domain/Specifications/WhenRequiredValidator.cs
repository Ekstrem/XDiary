namespace Journal.Domain.Specifications;

using Journal.Domain.Abstraction;

internal sealed class WhenRequiredValidator
{
    public bool IsSatisfiedBy(IJournalAnemicModel model)
        => model.Root.StartedAt != default;

    public string Reason => "Дата (startedAt) обязательна до Complete.";
}
