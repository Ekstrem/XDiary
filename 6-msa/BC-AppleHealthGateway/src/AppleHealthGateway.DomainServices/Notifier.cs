namespace AppleHealthGateway.DomainServices;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using AppleHealthGateway.Domain;
using XDiary.Contracts;
public sealed class Notifier : INotifier<IAppleHealthGateway>
{ public void Notify<TModel>(AggregateResult<IAppleHealthGateway, TModel> result) where TModel : IAnemicModel<IAppleHealthGateway> { } }