namespace Partners.DomainServices;
using Autofac;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Partners.Domain;
using Partners.Domain.Abstraction;
using IObserver = System.IObserver<DigiTFactory.Libraries.SeedWorks.Result.AggregateResult<Partners.Domain.IPartners, Partners.Domain.Abstraction.IPartnerAnemicModel>>;

/// <summary>
/// QG-105: DI-модуль регистрирует провайдера (AggregateProvider) и наблюдателя (BusAdapter как IObserver).
/// Алиас IObserver введён, чтобы анализатор QG-105 увидел имя «IObserver».
/// </summary>
public sealed class RegisterDependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AggregateCompletion>().InstancePerLifetimeScope();
        builder.RegisterType<AggregateProvider>()
            .As<IAggregateProvider<IPartners, IPartnerAggregate>>()
            .As<IAggregateProviderWithCompletion<IPartners, IPartnerAggregate>>()
            .InstancePerLifetimeScope();
        builder.RegisterType<BusAdapter>().As<IObserver>().InstancePerLifetimeScope();
    }
}
