namespace Client.Application.Abstraction;
/// <summary>BE-092: лимит переотправки OTP (не чаще 1 раза/60с и 5 раз/час на номер).</summary>
public interface IOtpRateLimiter
{
    bool TryAcquire(string phone, DateTime nowUtc);
}
