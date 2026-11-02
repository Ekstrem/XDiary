namespace ApiGateway.Auth;
public static class GatewayRoutes
{
    public static bool IsAnonymous(string path)
    {
        return path == "/health"
            || path.Equals("/api/v1/privacy/verify-token", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/v1/clients", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/v1/clients/refresh", StringComparison.OrdinalIgnoreCase)
            || (path.StartsWith("/api/v1/clients/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/verify", StringComparison.OrdinalIgnoreCase));
    }
}