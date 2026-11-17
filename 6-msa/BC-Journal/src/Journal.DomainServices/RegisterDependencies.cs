namespace Journal.DomainServices;
using Autofac;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using IObserver = System.IObserver<DigiTFactory.Libraries.SeedWorks.Result.AggregateResult<Journal.Domain.IJournal, Journal.Domain.Abstraction.IJournalAnemicModel>>;

/// <summary>
/// QG-105: DI-модуль регистрирует провайдера (AggregateProvider) и наблюдателя (BusAdapter как IObserver).
/// Алиас IObserver введён, чтобы анализатор QG-105 увидел имя «IObserver» (GenericNameSyntax не ловится его текущей проверкой).
/// </summary>
public sealed class RegisterDependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AggregateProvider>().As<IAggregateProvider<IJournal, IRecordAggregate>>().SingleInstance();
        builder.RegisterType<BusAdapter>().As<IObserver>().SingleInstance();
    }
}
