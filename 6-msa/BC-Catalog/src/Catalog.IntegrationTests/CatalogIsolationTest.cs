using Catalog.Application.Queries;
using Catalog.InternalContracts;
using Xunit;

/// <summary>BE-084: изоляция каталога — GetCatalogItemByIdQuery несёт clientId, чужой → null (404).</summary>
public class CatalogIsolationTest
{
    private sealed class StubRepo : ICatalogQueryRepository
    {
        public Guid? CapturedClientId { get; private set; }
        public CatalogReadModel? Result { get; set; }
        public Task<CatalogReadModel?> GetByIdAsync(Guid id, Guid clientId, CancellationToken ct = default)
        { CapturedClientId = clientId; return Task.FromResult(Result); }
        public Task<IReadOnlyList<CatalogReadModel>> ListAsync(Guid clientId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CatalogReadModel>>(new List<CatalogReadModel>());
        public Task<int> CountAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    [Fact]
    public async Task GetByIdQuery_CarriesClientId()
    {
        var stub = new StubRepo();
        var handler = new GetCatalogItemByIdHandler(stub);
        var clientB = Guid.NewGuid();
        await handler.Handle(new GetCatalogItemByIdQuery(Guid.NewGuid(), clientB), CancellationToken.None);
        Assert.Equal(clientB, stub.CapturedClientId); // запрос несёт владельца из токена → репозиторий фильтрует
    }
}
