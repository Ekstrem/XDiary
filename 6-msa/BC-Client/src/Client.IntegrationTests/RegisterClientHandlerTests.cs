using Client.Application.Commands;
using Client.Domain;
using Client.Domain.Abstraction;
using Client.InternalContracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Xunit;
using XDiary.Contracts;

public class RegisterClientHandlerTests
{
    private sealed class FakeRepo : IRepository<IClients, IClientAnemicModel>
    {
        public Task<IClientAnemicModel?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IClientAnemicModel?>(null);
        public Task SaveAsync(AggregateResult<IClients, IClientAnemicModel> result, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class FakeQuery : IClientQueryRepository
    {
        public bool HasPhone { get; set; }
        public Task<ClientReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<ClientReadModel?>(null);
        public Task<IReadOnlyList<ClientReadModel>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ClientReadModel>>(Array.Empty<ClientReadModel>());
        public Task<ClientReadModel?> GetByPhoneAsync(string phone, CancellationToken ct = default) => Task.FromResult<ClientReadModel?>(HasPhone ? new ClientReadModel { Phone = phone } : null);
    }
    private sealed class FakeSender : Client.Application.Abstraction.IOtpSender
    {
        public Task SendAsync(string phone, string otp, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task Register_DuplicatePhone_ReturnsPhoneTaken()
    {
        var handler = new RegisterClientHandler(new FakeRepo(), new FakeQuery { HasPhone = true }, new FakeSender(), null!, Microsoft.Extensions.Logging.Abstractions.NullLogger<RegisterClientHandler>.Instance);
        var res = await handler.Handle(new RegisterClientCommand("+79990000000"), CancellationToken.None);
        Assert.False(res.IsSuccess());
        Assert.Contains("phone_taken", res.ErrorMessage());
        Assert.DoesNotContain("Postgres", res.ErrorMessage());
    }
}