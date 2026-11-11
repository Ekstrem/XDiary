using ApiGateway.Auth;
using Xunit;

public class GatewayRoutesTests
{
    [Theory]
    [InlineData("GET", "/health")]
    [InlineData("POST", "/api/v1/clients")]
    [InlineData("POST", "/api/v1/clients/refresh")]
    [InlineData("POST", "/api/v1/clients/00000000-0000-0000-0000-000000000000/verify")]
    [InlineData("POST", "/api/v1/privacy/verify-token")]
    public void IsAnonymous_AnonymousPaths_True(string method, string path) => Assert.True(GatewayRoutes.IsAnonymous(method, path));

    [Theory]
    [InlineData("GET", "/api/v1/clients")]          // GET списка — не анонимен (deny-by-default)
    [InlineData("GET", "/api/v1/partners")]
    [InlineData("GET", "/api/v1/clients/me")]
    [InlineData("POST", "/api/v1/privacy/pin/set")]
    [InlineData("POST", "/api/v1/privacy/token")]
    [InlineData("POST", "/api/v1/clients/refresh/extra")]
    public void IsAnonymous_ProtectedPaths_False(string method, string path) => Assert.False(GatewayRoutes.IsAnonymous(method, path));
}
