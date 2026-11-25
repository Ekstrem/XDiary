namespace GarminGateway.Domain.Implementation;
using GarminGateway.Domain.Abstraction;

/// <summary>BE-142: ApiKey обязателен; OauthToken — серверный (null/маркер), не пользовательский ввод.</summary>
internal sealed record Credentials(string ApiKey, string? OauthToken, string TokenExpiry) : ICredentials
{
    public static ICredentials CreateInstance(string apiKey, string? oauthToken, string tokenExpiry) => new Credentials(apiKey, oauthToken, tokenExpiry);
}
