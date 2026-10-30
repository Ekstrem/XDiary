namespace DailyMetrics.DomainServices;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DailyMetrics.Domain;
using XDiary.Contracts;
public sealed class Notifier : INotifier<IDailyMetrics>
{ public void Notify<TModel>(AggregateResult<IDailyMetrics, TModel> result) where TModel : IAnemicModel<IDailyMetrics> { } }