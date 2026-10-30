namespace Journal.DomainServices;
using Autofac;
using Journal.Domain;
using XDiary.Contracts;
public sealed class RegisterDependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AggregateProvider>().AsSelf().SingleInstance();
        builder.RegisterType<BusAdapter>().As<IBusAdapter<IJournal>>().SingleInstance();
        builder.RegisterType<Notifier>().As<INotifier<IJournal>>().SingleInstance();
    }
}