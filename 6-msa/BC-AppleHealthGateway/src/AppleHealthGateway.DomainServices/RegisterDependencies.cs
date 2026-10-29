using Autofac;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Monads;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;

namespace AppleHealthGateway.DomainServices;

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
                .As<IAggregateProvider<IAppleHealthGateway, IAppleImportAggregate>>()
                .As<IAppleImportAggregateProvider>())
            .Do(b => b
                .RegisterType<BusAdapter>()
                .As<IObserver<AggregateResult<IAppleHealthGateway, IAnemicModel<IAppleHealthGateway>>>>());
}
