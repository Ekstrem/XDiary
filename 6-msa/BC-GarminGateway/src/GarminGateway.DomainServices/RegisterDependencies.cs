namespace GarminGateway.DomainServices;
using Autofac;
using GarminGateway.Domain;
using XDiary.Contracts;
public sealed class RegisterDependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AggregateProvider>().AsSelf().SingleInstance();
    }
}