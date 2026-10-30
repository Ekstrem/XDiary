namespace Privacy.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface IPinCredentialAnemicModel : IAnemicModel<IPrivacy>
{
    IPinCredentialRoot Root { get; }
    bool Locked { get; }
}