namespace Portability.DomainServices;
using Autofac;
using Portability.Domain;
using XDiary.Contracts;
public sealed class RegisterDependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AggregateProvider>().AsSelf().SingleInstance();
        builder.RegisterType<BusAdapter>().As<IBusAdapter<IPortability>>().SingleInstance();
        builder.RegisterType<Notifier>().As<INotifier<IPortability>>().SingleInstance();
    }
}