namespace DailyMetrics.DomainServices;
using Autofac;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;
using IObserver = System.IObserver<DigiTFactory.Libraries.SeedWorks.Result.AggregateResult<DailyMetrics.Domain.IDailyMetrics, DailyMetrics.Domain.Abstraction.IDailyMetricsAnemicModel>>;

/// <summary>QG-105: регистрирует провайдера и наблюдателя.</summary>
public sealed class RegisterDependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AggregateCompletion>().InstancePerLifetimeScope();
        builder.RegisterType<AggregateProvider>()
            .As<IAggregateProvider<IDailyMetrics, IDailyMetricsAggregate>>()
            .As<IAggregateProviderWithCompletion<IDailyMetrics, IDailyMetricsAggregate>>()
            .InstancePerLifetimeScope();
        builder.RegisterType<BusAdapter>().As<IObserver>().InstancePerLifetimeScope();
    }
}
