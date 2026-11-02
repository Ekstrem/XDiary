namespace Client.Application.Abstraction;
public interface IOtpSender
{
    Task SendAsync(string phone, string otp, CancellationToken ct = default);
}