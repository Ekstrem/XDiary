namespace SomaticSeries.DomainServices;
using Autofac;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;
using IObserver = System.IObserver<DigiTFactory.Libraries.SeedWorks.Result.AggregateResult<SomaticSeries.Domain.ISomaticSeries, SomaticSeries.Domain.Abstraction.ISomaticSeriesAnemicModel>>;

/// <summary>QG-105: регистрирует провайдера и наблюдателя.</summary>
public sealed class RegisterDependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AggregateCompletion>().InstancePerLifetimeScope();
        builder.RegisterType<AggregateProvider>()
            .As<IAggregateProvider<ISomaticSeries, ISomaticSeriesAggregate>>()
            .As<IAggregateProviderWithCompletion<ISomaticSeries, ISomaticSeriesAggregate>>()
            .InstancePerLifetimeScope();
        builder.RegisterType<BusAdapter>().As<IObserver>().InstancePerLifetimeScope();
    }
}
