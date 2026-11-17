namespace GarminGateway.Domain.Implementation;
using GarminGateway.Domain.Abstraction;
public sealed record Credentials(string ApiKey, string OauthToken, string TokenExpiry) : ICredentials
{
    public static ICredentials CreateInstance(string apiKey, string oauthToken, string tokenExpiry) => new Credentials(apiKey, oauthToken, tokenExpiry);
}