namespace AppleHealthGateway.DomainServices;
using Autofac;
using AppleHealthGateway.Domain;
using XDiary.Contracts;
public sealed class RegisterDependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AggregateProvider>().AsSelf().SingleInstance();
        builder.RegisterType<BusAdapter>().As<IBusAdapter<IAppleHealthGateway>>().SingleInstance();
        builder.RegisterType<Notifier>().As<INotifier<IAppleHealthGateway>>().SingleInstance();
    }
}