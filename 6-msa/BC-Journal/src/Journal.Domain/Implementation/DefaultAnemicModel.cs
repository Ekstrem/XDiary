namespace Journal.Domain.Implementation;

using Journal.Domain.Abstraction;

// QG-102: фабрика «пустой» (default) модели — начальное состояние агрегата при промахе репозитория.
// Доступна DomainServices через InternalsVisibleTo (реализация остаётся internal — QG-020).
internal static class DefaultAnemicModel
{
    public static IJournalAnemicModel Create(Guid id) => AnemicModel.Create(id);
}
