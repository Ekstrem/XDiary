using Client.Application.Commands;
using Client.Application.Abstraction;
using Client.Domain;
using Client.Domain.Abstraction;
using Client.Domain.Implementation;
using Client.InternalContracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Xunit;
using XDiary.Contracts;

public class RegisterClientHandlerTests
{
    private sealed class FakeRepo : IRepository<IClients, IClientAnemicModel>
    {
        public IClientAnemicModel? Existing { get; set; }
        public bool ThrowDuplicateOnSave { get; set; }
        public Task<IClientAnemicModel?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Existing is not null && Existing.Id == id ? Existing : null);
        public Task SaveAsync(AggregateResult<IClients, IClientAnemicModel> result, CancellationToken ct = default)
        { if (ThrowDuplicateOnSave) throw new DuplicatePhoneException(); return Task.CompletedTask; }
    }
    private sealed class FakeQuery : IClientQueryRepository
    {
        public Guid? ExistingId { get; set; }
        public Task<ClientReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<ClientReadModel?>(null);
        public Task<IReadOnlyList<ClientReadModel>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ClientReadModel>>(Array.Empty<ClientReadModel>());
        public Task<ClientReadModel?> GetByPhoneAsync(string phone, CancellationToken ct = default) => Task.FromResult<ClientReadModel?>(ExistingId is Guid g ? new ClientReadModel { Id = g, Phone = phone } : null);
    }
    private sealed class FakeSender : IOtpSender
    {
        public bool Ok { get; set; } = true;
        public Task<bool> SendAsync(string phone, string otp, CancellationToken ct = default) => Task.FromResult(Ok);
    }
    private sealed class FakeRateLimiter : IOtpRateLimiter
    {
        public bool Allow { get; set; } = true;
        public bool TryAcquire(string phone, DateTime nowUtc) => Allow;
    }
    private sealed class FakeConfig : IConfiguration
    {
        public string? this[string key] { get => null; set { } }
        public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();
        public IChangeToken GetReloadToken() => null!;
        public IConfigurationSection GetSection(string key) => null!;
    }

    private static RegisterClientHandler MakeHandler(FakeRepo repo, FakeQuery query, FakeSender sender, FakeRateLimiter limiter)
        => new(repo, query, sender, limiter, new FakeConfig(), Microsoft.Extensions.Logging.Abstractions.NullLogger<RegisterClientHandler>.Instance);

    [Fact]
    public async Task Register_ExistingPhone_ReissuesOtp_And_MarksExisting()
    {
        var id = Guid.NewGuid();
        var repo = new FakeRepo { Existing = new AnemicModel { Id = id, Root = ClientRoot.CreateInstance("+79990000000"), OtpCode = "old" } };
        var handler = MakeHandler(repo, new FakeQuery { ExistingId = id }, new FakeSender(), new FakeRateLimiter());
        var res = await handler.Handle(new RegisterClientCommand("+79990000000"), CancellationToken.None);
        Assert.True(res.Result.IsSuccess());
        Assert.True(res.Existing);               // BE-092: контроллер отдаст 409
        Assert.NotEqual("old", res.Result.Model().OtpCode);
    }

    [Fact]
    public async Task Register_Race_DuplicatePhone_ReturnsPhoneTaken()
    {
        var repo = new FakeRepo { ThrowDuplicateOnSave = true };
        var handler = MakeHandler(repo, new FakeQuery { ExistingId = null }, new FakeSender(), new FakeRateLimiter());
        var res = await handler.Handle(new RegisterClientCommand("+79990000000"), CancellationToken.None);
        Assert.False(res.Result.IsSuccess());
        Assert.Contains("phone_taken", res.Result.ErrorMessage());
    }

    [Fact]
    public async Task Register_ChannelUnavailable_DeliveryOkFalse_But_Succeeds()
    {
        var handler = MakeHandler(new FakeRepo(), new FakeQuery { ExistingId = null }, new FakeSender { Ok = false }, new FakeRateLimiter());
        var res = await handler.Handle(new RegisterClientCommand("+79990000000"), CancellationToken.None);
        Assert.True(res.Result.IsSuccess());     // BE-092: недоступный канал — не отказ регистрации
        Assert.False(res.DeliveryOk);
    }

    [Fact]
    public async Task Register_RateLimited_Signals429()
    {
        var handler = MakeHandler(new FakeRepo(), new FakeQuery { ExistingId = null }, new FakeSender(), new FakeRateLimiter { Allow = false });
        var res = await handler.Handle(new RegisterClientCommand("+79990000000"), CancellationToken.None);
        Assert.True(res.RateLimited);
    }
}
