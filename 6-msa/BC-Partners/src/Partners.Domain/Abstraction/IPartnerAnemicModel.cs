namespace Partners.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface IPartnerAnemicModel : IAnemicModel<IPartners>
{
    IPartnerRoot Root { get; }
    string Photo { get; }
    string Gender { get; }
    string Notes { get; }
}