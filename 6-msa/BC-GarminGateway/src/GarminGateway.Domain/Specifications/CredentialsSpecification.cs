namespace GarminGateway.Domain.Specifications;
using GarminGateway.Domain.Abstraction;

/// <summary>BE-142 (один токен): обязателен только ApiKey; OauthToken сервер добывает сам.</summary>
internal sealed class CredentialsSpecification
{
    public bool IsSatisfiedBy(ICredentials c) => !string.IsNullOrWhiteSpace(c.ApiKey);
    public string Reason => "apiKey обязателен.";
}
