namespace Catalog.DomainServices;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Catalog.Domain;
using XDiary.Contracts;
public sealed class Notifier : INotifier<ICatalog>
{ public void Notify<TModel>(AggregateResult<ICatalog, TModel> result) where TModel : IAnemicModel<ICatalog> { } }