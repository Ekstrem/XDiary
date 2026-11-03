namespace ApiGateway.Auth;
public static class GatewayRoutes
{
    /// <summary>
    /// Анонимные маршруты. ВАЖНО (P0 2026-11-10): `/api/v1/clients` анонимен ТОЛЬКО для POST —
    /// это онбординг (создание клиента). GET того же пути отдавал список клиентов с телефонами
    /// без токена (проверено на проде: 200, 9 записей с `phone`), поэтому метод теперь часть
    /// условия: GET идёт через JWT и получает 401.
    /// </summary>
    public static bool IsAnonymous(string path, string method)
    {
        if (path == "/health")
        {
            return true;
        }

        if (path.Equals("/api/v1/privacy/verify-token", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/v1/clients/refresh", StringComparison.OrdinalIgnoreCase)
            || (path.StartsWith("/api/v1/clients/", StringComparison.OrdinalIgnoreCase)
                && path.EndsWith("/verify", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // онбординг: только создание клиента, не чтение списка
        if (path.Equals("/api/v1/clients", StringComparison.OrdinalIgnoreCase))
        {
            return HttpMethods.IsPost(method);
        }

        return false;
    }
}