using DigiTFactory.Libraries.SeedWorks.Definition;
namespace Client.Domain;
public interface IClients : IBoundedContext { }
public class ClientsBoundedContextDescription : IBoundedContextDescription
{
    public string ContextName => "Clients";
    public int MicroserviceVersion => 1;
}