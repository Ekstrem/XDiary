namespace SomaticSeries.DomainServices;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using SomaticSeries.Domain;
using XDiary.Contracts;
public sealed class Notifier : INotifier<ISomaticSeries>
{ public void Notify<TModel>(AggregateResult<ISomaticSeries, TModel> result) where TModel : IAnemicModel<ISomaticSeries> { } }