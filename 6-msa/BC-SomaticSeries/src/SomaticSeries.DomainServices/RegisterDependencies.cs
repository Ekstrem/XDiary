namespace SomaticSeries.DomainServices;
using Autofac;
using SomaticSeries.Domain;
using XDiary.Contracts;
public sealed class RegisterDependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AggregateProvider>().AsSelf().SingleInstance();
        builder.RegisterType<BusAdapter>().As<IBusAdapter<ISomaticSeries>>().SingleInstance();
        builder.RegisterType<Notifier>().As<INotifier<ISomaticSeries>>().SingleInstance();
    }
}