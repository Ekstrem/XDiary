namespace Client.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface IClientAnemicModel : IAnemicModel<IClients>
{
    IClientRoot Root { get; }
    string OtpCode { get; }
    bool Verified { get; }
    Guid? LinkedPartnerId { get; }
}