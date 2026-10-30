namespace DailyMetrics.DomainServices;
using Autofac;
using DailyMetrics.Domain;
using XDiary.Contracts;
public sealed class RegisterDependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AggregateProvider>().AsSelf().SingleInstance();
        builder.RegisterType<BusAdapter>().As<IBusAdapter<IDailyMetrics>>().SingleInstance();
        builder.RegisterType<Notifier>().As<INotifier<IDailyMetrics>>().SingleInstance();
    }
}