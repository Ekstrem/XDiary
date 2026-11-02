using ApiGateway.Auth;
using Xunit;

public class GatewayRoutesTests
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/api/v1/clients")]
    [InlineData("/api/v1/clients/refresh")]
    [InlineData("/api/v1/clients/00000000-0000-0000-0000-000000000000/verify")]
    [InlineData("/api/v1/privacy/verify-token")]
    public void IsAnonymous_AnonymousPaths_True(string path) => Assert.True(GatewayRoutes.IsAnonymous(path));

    [Theory]
    [InlineData("/api/v1/partners")]
    [InlineData("/api/v1/clients/me")]
    [InlineData("/api/v1/privacy/pin/set")]
    [InlineData("/api/v1/privacy/token")]
    [InlineData("/api/v1/clients/refresh/extra")]
    public void IsAnonymous_ProtectedPaths_False(string path) => Assert.False(GatewayRoutes.IsAnonymous(path));
}