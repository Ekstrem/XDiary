namespace Journal.Domain.Specifications;

using Journal.Domain.Abstraction;

internal sealed class WhenRequiredValidator
{
    public bool IsSatisfiedBy(IJournalAnemicModel model)
        => !string.IsNullOrWhiteSpace(model.Root.StartedAt);

    public string Reason => "Дата (startedAt) обязательна до Complete.";
}
