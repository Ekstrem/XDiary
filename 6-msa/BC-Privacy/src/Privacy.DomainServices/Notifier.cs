namespace Privacy.DomainServices;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Privacy.Domain;
using XDiary.Contracts;
public sealed class Notifier : INotifier<IPrivacy>
{ public void Notify<TModel>(AggregateResult<IPrivacy, TModel> result) where TModel : IAnemicModel<IPrivacy> { } }