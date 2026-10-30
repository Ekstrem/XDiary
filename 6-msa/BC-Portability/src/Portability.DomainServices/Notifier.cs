namespace Portability.DomainServices;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Portability.Domain;
using XDiary.Contracts;
public sealed class Notifier : INotifier<IPortability>
{ public void Notify<TModel>(AggregateResult<IPortability, TModel> result) where TModel : IAnemicModel<IPortability> { } }