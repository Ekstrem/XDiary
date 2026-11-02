namespace Privacy.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface IPinCredentialRoot : IValueObject { Guid ClientId { get; } string Salt { get; } string Hash { get; } int Iterations { get; } }