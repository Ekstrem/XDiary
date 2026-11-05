namespace SomaticSeries.DomainServices;
using Autofac;
using SomaticSeries.Domain;
using XDiary.Contracts;
public sealed class RegisterDependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AggregateProvider>().AsSelf().SingleInstance();
    }
}