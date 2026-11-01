namespace Client.DomainServices;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Client.Domain;
using XDiary.Contracts;
public sealed class Notifier : INotifier<IClients>
{
    public void Notify<TModel>(AggregateResult<IClients, TModel> result) where TModel : IAnemicModel<IClients> { }
}