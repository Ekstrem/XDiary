namespace GarminGateway.DomainServices;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using GarminGateway.Domain;
using XDiary.Contracts;
public sealed class Notifier : INotifier<IGarminGateway>
{ public void Notify<TModel>(AggregateResult<IGarminGateway, TModel> result) where TModel : IAnemicModel<IGarminGateway> { } }