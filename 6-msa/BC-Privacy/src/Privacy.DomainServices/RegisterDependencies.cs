namespace Privacy.DomainServices;
using Autofac;
using Privacy.Domain;
using XDiary.Contracts;
public sealed class RegisterDependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AggregateProvider>().AsSelf().SingleInstance();
        builder.RegisterType<BusAdapter>().As<IBusAdapter<IPrivacy>>().SingleInstance();
        builder.RegisterType<Notifier>().As<INotifier<IPrivacy>>().SingleInstance();
    }
}