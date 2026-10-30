namespace GarminGateway.Domain.Implementation;
using GarminGateway.Domain.Abstraction;
public sealed class Credentials : ICredentials
{
    public string ApiKey { get; private set; } = string.Empty;
    public string OauthToken { get; private set; } = string.Empty;
    public string TokenExpiry { get; private set; } = string.Empty;
    private Credentials() { }
    public static ICredentials CreateInstance(string apiKey, string oauthToken, string tokenExpiry) => new Credentials { ApiKey = apiKey, OauthToken = oauthToken, TokenExpiry = tokenExpiry };
}