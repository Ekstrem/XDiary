using Autofac;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Monads;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;

namespace Journal.DomainServices;

/// <summary>
/// Autofac-модуль: регистрация доменных сервисов журнала.
/// </summary>
public class RegisterDependencies : Module
{
    protected override void Load(ContainerBuilder builder)
        => RegisterDomainServices(builder);

    private static ContainerBuilder RegisterDomainServices(ContainerBuilder builder)
        => builder
            .Do(b => b
                .RegisterType<AggregateProvider>()
                .As<IAggregateProvider<IJournal, IRecordAggregate>>()
                .As<IJournalAggregateProvider>())
            .Do(b => b
                .RegisterType<BusAdapter>()
                .As<IObserver<AggregateResult<IJournal, IAnemicModel<IJournal>>>>());
}
