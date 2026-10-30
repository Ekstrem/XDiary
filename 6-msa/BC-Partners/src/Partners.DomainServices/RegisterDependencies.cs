namespace Partners.DomainServices;
using Autofac;
using Partners.Domain;
using XDiary.Contracts;
public sealed class RegisterDependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AggregateProvider>().AsSelf().SingleInstance();
        builder.RegisterType<BusAdapter>().As<IBusAdapter<IPartners>>().SingleInstance();
        builder.RegisterType<Notifier>().As<INotifier<IPartners>>().SingleInstance();
    }
}