using Autofac;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Monads;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;

namespace DailyMetrics.DomainServices;

/// <summary>
/// Autofac-РјРѕРґСѓР»СЊ: СЂРµРіРёСЃС‚СЂР°С†РёСЏ РґРѕРјРµРЅРЅС‹С… СЃРµСЂРІРёСЃРѕРІ Р¶СѓСЂРЅР°Р»Р°.
/// </summary>
public class RegisterDependencies : Module
{
    protected override void Load(ContainerBuilder builder)
        => RegisterDomainServices(builder);

    private static ContainerBuilder RegisterDomainServices(ContainerBuilder builder)
        => builder
            .Do(b => b
                .RegisterType<AggregateProvider>()
                .As<IAggregateProvider<IDailyMetrics, IDailyMetricsAggregate>>()
                .As<IDailyMetricsAggregateProvider>())
            .Do(b => b
                .RegisterType<BusAdapter>()
                .As<IObserver<AggregateResult<IDailyMetrics, IAnemicModel<IDailyMetrics>>>>());
}
