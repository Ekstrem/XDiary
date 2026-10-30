namespace GarminGateway.DomainServices;
using Autofac;
using GarminGateway.Domain;
using XDiary.Contracts;
public sealed class RegisterDependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AggregateProvider>().AsSelf().SingleInstance();
        builder.RegisterType<BusAdapter>().As<IBusAdapter<IGarminGateway>>().SingleInstance();
        builder.RegisterType<Notifier>().As<INotifier<IGarminGateway>>().SingleInstance();
    }
}