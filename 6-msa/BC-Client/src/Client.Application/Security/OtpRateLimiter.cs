namespace Client.Application.Security;
using Client.Application.Abstraction;
/// <summary>
/// BE-092: in-memory rate limiter. Ключ — телефон. Окно 1 час, не чаще 60с между
/// попытками и не более 5 в окне. Экземпляр — singleton (per-service, без общего стора).
/// </summary>
public sealed class OtpRateLimiter : IOtpRateLimiter
{
    private readonly object _sync = new();
    private readonly Dictionary<string, List<DateTime>> _attempts = new();
    private const int MaxPerHour = 5;
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    public bool TryAcquire(string phone, DateTime nowUtc)
    {
        lock (_sync)
        {
            if (!_attempts.TryGetValue(phone, out var list)) { list = new List<DateTime>(); _attempts[phone] = list; }
            list.RemoveAll(t => nowUtc - t > Window);
            if (list.Count >= MaxPerHour) return false;
            if (list.Count > 0 && nowUtc - list[^1] < MinInterval) return false;
            list.Add(nowUtc);
            return true;
        }
    }
}
