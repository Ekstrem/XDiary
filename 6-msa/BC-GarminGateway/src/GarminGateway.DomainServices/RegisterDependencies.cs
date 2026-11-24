namespace GarminGateway.DomainServices;
using Autofac;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;
using IObserver = System.IObserver<DigiTFactory.Libraries.SeedWorks.Result.AggregateResult<GarminGateway.Domain.IGarminGateway, GarminGateway.Domain.Abstraction.IGarminGatewayAnemicModel>>;

/// <summary>QG-105: регистрирует провайдера и наблюдателя.</summary>
public sealed class RegisterDependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AggregateCompletion>().InstancePerLifetimeScope();
        builder.RegisterType<AggregateProvider>()
            .As<IAggregateProvider<IGarminGateway, IGarminGatewayAggregate>>()
            .As<IAggregateProviderWithCompletion<IGarminGateway, IGarminGatewayAggregate>>()
            .InstancePerLifetimeScope();
        builder.RegisterType<BusAdapter>().As<IObserver>().InstancePerLifetimeScope();
    }
}
