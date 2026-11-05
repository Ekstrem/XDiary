namespace AppleHealthGateway.DomainServices;
using Autofac;
using AppleHealthGateway.Domain;
using XDiary.Contracts;
public sealed class RegisterDependencies : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AggregateProvider>().AsSelf().SingleInstance();
    }
}