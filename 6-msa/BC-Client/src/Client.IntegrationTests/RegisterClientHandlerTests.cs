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
        public bool MissFirst { get; set; }   // race: первый GetByPhoneAsync → null, повторный → ExistingId
        private int _phoneCalls;
        public Task<ClientReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<ClientReadModel?>(null);
        public Task<IReadOnlyList<ClientReadModel>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ClientReadModel>>(Array.Empty<ClientReadModel>());
        public Task<ClientReadModel?> GetByPhoneAsync(string phone, CancellationToken ct = default)
        {
            _phoneCalls++;
            var miss = MissFirst && _phoneCalls == 1;
            return Task.FromResult<ClientReadModel?>(!miss && ExistingId is Guid g ? new ClientReadModel { Id = g, Phone = phone } : null);
        }
    }
    private sealed class FakeSender : IOtpSender
    {
        public bool Ok { get; set; } = true;
        public int SentCount { get; private set; }
        public string? LastPhone { get; private set; }
        public Task<bool> SendAsync(string phone, string otp, CancellationToken ct = default) { SentCount++; LastPhone = phone; return Task.FromResult(Ok); }
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
    public async Task Register_Race_DuplicatePhone_ReissuesOtp_And_MarksExisting()
    {
        var id = Guid.NewGuid();
        var repo = new FakeRepo { ThrowDuplicateOnSave = true, Existing = new AnemicModel { Id = id, Root = ClientRoot.CreateInstance("+79990000000"), OtpCode = "old" } };
        // MissFirst: первый GetByPhoneAsync (до INSERT) → null, повторный (в catch) → находит существующего.
        var handler = MakeHandler(repo, new FakeQuery { ExistingId = id, MissFirst = true }, new FakeSender(), new FakeRateLimiter());
        var res = await handler.Handle(new RegisterClientCommand("+79990000000"), CancellationToken.None);
        Assert.True(res.Result.IsSuccess());   // BE-092: не 400/200, а путь Existing = true
        Assert.True(res.Existing);             // → контроллер отдаёт 409 + clientId
        Assert.NotEqual("old", res.Result.Model().OtpCode);
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

    [Fact]
    public async Task Register_EmptyPhone_FailsBeforeSend()
    {
        var sender = new FakeSender();
        var handler = MakeHandler(new FakeRepo(), new FakeQuery { ExistingId = null }, sender, new FakeRateLimiter());
        var res = await handler.Handle(new RegisterClientCommand(""), CancellationToken.None);
        Assert.False(res.Result.IsSuccess());   // BE-092 (@TL 08:02): валидация ДО отправки
        Assert.Equal(0, sender.SentCount);      // OTP не отправлен на пустой номер
    }

    [Fact]
    public void VerifyOtp_NullExpiry_ReturnsExpired()
    {
        // BE-047 (@AR 14:07): пустой otpExpiresAt (nullable) не должен проходить бессрочно — null = истёк.
        var model = new AnemicModel { Id = Guid.NewGuid(), Root = ClientRoot.CreateInstance("+79990000000"), OtpCode = "123456", OtpExpiresAt = null };
        var res = Aggregate.CreateInstance(model).VerifyOtp("123456");
        Assert.False(res.IsSuccess());
        Assert.Contains("истёк", res.ErrorMessage());
    }

    // TE-097 (канон AR-025): мусорный номер не должен доезжать до отправки OTP.
    [Theory]
    [InlineData("abc")]
    [InlineData("123")]
    [InlineData("не телефон")]
    [InlineData("+0123456789")]
    [InlineData("+7900000000")]  // +7 + 9 цифр (всего 10) — несуществующий номер (@AR 23:53)
    public async Task Register_InvalidPhoneFormat_FailsBeforeSend(string phone)
    {
        var sender = new FakeSender();
        var handler = MakeHandler(new FakeRepo(), new FakeQuery { ExistingId = null }, sender, new FakeRateLimiter());
        var res = await handler.Handle(new RegisterClientCommand(phone), CancellationToken.None);
        Assert.False(res.Result.IsSuccess());
        Assert.Equal(0, sender.SentCount);
    }

    // TE-097: уникальность считается по НОРМАЛИЗОВАННОЙ форме — иначе форматирование обходит инвариант.
    [Theory]
    [InlineData("+7 900 000-00-00", "+79000000000")]
    [InlineData("8 (900) 000-00-00", "+79000000000")]
    public async Task Register_Formats_NormalizedBeforeSend(string input, string expected)
    {
        var sender = new FakeSender();
        var handler = MakeHandler(new FakeRepo(), new FakeQuery { ExistingId = null }, sender, new FakeRateLimiter());
        var res = await handler.Handle(new RegisterClientCommand(input), CancellationToken.None);
        Assert.True(res.Result.IsSuccess());
        Assert.Equal(expected, sender.LastPhone);
    }
}
