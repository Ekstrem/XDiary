namespace Partners.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface IPartnerRoot : IAggregateRoot<IPartners> { string Name { get; } }