namespace GarminGateway.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface ICredentials : IValueObject { string ApiKey { get; } string OauthToken { get; } string TokenExpiry { get; } }