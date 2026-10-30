namespace Catalog.DomainServices;
using Autofac;
using Catalog.Domain;
using XDiary.Contracts;
public sealed class RegisterDependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AggregateProvider>().AsSelf().SingleInstance();
        builder.RegisterType<BusAdapter>().As<IBusAdapter<ICatalog>>().SingleInstance();
        builder.RegisterType<Notifier>().As<INotifier<ICatalog>>().SingleInstance();
    }
}