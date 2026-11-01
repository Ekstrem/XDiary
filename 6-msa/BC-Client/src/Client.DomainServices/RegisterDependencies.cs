namespace Client.DomainServices;
using Autofac;
using Client.Domain;
using XDiary.Contracts;
public sealed class RegisterDependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AggregateProvider>().AsSelf().SingleInstance();
        builder.RegisterType<BusAdapter>().As<IBusAdapter<IClients>>().SingleInstance();
        builder.RegisterType<Notifier>().As<INotifier<IClients>>().SingleInstance();
    }
}