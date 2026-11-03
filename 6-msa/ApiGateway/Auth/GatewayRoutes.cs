namespace ApiGateway.Auth;
public static class GatewayRoutes
{
    /// <summary>
    /// Анонимные маршруты — по паре (метод, путь), как в ADR-026 (@AR 2026-11-10 07:14).
    ///
    /// Почему пара, а не путь: один URL может нести два класса доступа. `/api/v1/clients`
    /// — это `POST` (регистрация, обязана быть анонимной) и `GET` (список клиентов,
    /// не должен быть доступен без токена). Правило «по пути» их склеило, и `GET`
    /// отдавал список с телефонами (проверено на dev; закрыто 2026-11-10).
    /// </summary>
    public static bool IsAnonymous(string method, string path)
    {
        // health — служебный, без метода
        if (path == "/health")
        {
            return true;
        }

        if (!HttpMethods.IsPost(method))
        {
            return false;      // всё, что не POST, требует JWT (deny-by-default)
        }

        if (path.Equals("/api/v1/clients", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/v1/clients/refresh", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/v1/privacy/verify-token", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.StartsWith("/api/v1/clients/", StringComparison.OrdinalIgnoreCase)
               && path.EndsWith("/verify", StringComparison.OrdinalIgnoreCase);
    }
}
