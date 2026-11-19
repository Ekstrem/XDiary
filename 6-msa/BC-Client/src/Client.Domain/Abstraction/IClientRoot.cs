namespace Client.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface IClientRoot : IAggregateRoot<IClients> { string Phone { get; } }