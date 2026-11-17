namespace Privacy.DomainServices;
using Autofac;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Privacy.Domain;
using Privacy.Domain.Abstraction;
using IObserver = System.IObserver<DigiTFactory.Libraries.SeedWorks.Result.AggregateResult<Privacy.Domain.IPrivacy, Privacy.Domain.Abstraction.IPinCredentialAnemicModel>>;

/// <summary>QG-105: регистрирует провайдера и наблюдателя.</summary>
public sealed class RegisterDependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AggregateProvider>().As<IAggregateProvider<IPrivacy, IPinCredentialAggregate>>().InstancePerLifetimeScope();
        builder.RegisterType<BusAdapter>().As<IObserver>().InstancePerLifetimeScope();
    }
}
