namespace GarminGateway.Domain.Specifications;
using GarminGateway.Domain.Abstraction;
internal sealed class CredentialsValidator
{
    public bool IsSatisfiedBy(ICredentials c) => !string.IsNullOrWhiteSpace(c.ApiKey) && !string.IsNullOrWhiteSpace(c.OauthToken);
    public string Reason => "apiKey и oauthToken обязательны.";
}