namespace AppleHealthGateway.DomainServices;
using Autofac;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;
using IObserver = System.IObserver<DigiTFactory.Libraries.SeedWorks.Result.AggregateResult<AppleHealthGateway.Domain.IAppleHealthGateway, AppleHealthGateway.Domain.Abstraction.IAppleHealthGatewayAnemicModel>>;

/// <summary>QG-105: регистрирует провайдера и наблюдателя.</summary>
public sealed class RegisterDependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AggregateProvider>().As<IAggregateProvider<IAppleHealthGateway, IAppleHealthGatewayAggregate>>().InstancePerLifetimeScope();
        builder.RegisterType<BusAdapter>().As<IObserver>().InstancePerLifetimeScope();
    }
}
