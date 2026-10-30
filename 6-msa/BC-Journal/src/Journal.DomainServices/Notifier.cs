namespace Journal.DomainServices;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using XDiary.Contracts;
public sealed class Notifier : INotifier<IJournal>
{
    public void Notify<TModel>(AggregateResult<IJournal, TModel> result) where TModel : IAnemicModel<IJournal> { }
}