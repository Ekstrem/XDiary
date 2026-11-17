namespace Client.Application.Queries;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using MediatR;
using Client.Domain;
using Client.InternalContracts;
public sealed record ListClientsQuery : IRequest<IReadOnlyList<ClientReadModel>>;
public sealed record GetClientByIdQuery(Guid AggregateId) : IRequest<ClientReadModel?>;
public sealed record GetClientByPhoneQuery(string Phone) : IRequest<ClientReadModel?>;
public sealed class ListClientsHandler : IRequestHandler<ListClientsQuery, IReadOnlyList<ClientReadModel>>, IQueryHandler
{
    private readonly IReadRepository<IClients, ClientReadModel> _read;
    public ListClientsHandler(IReadRepository<IClients, ClientReadModel> read) => _read = read;
    public async Task<IReadOnlyList<ClientReadModel>> Handle(ListClientsQuery r, CancellationToken ct)
        => (await _read.GetAllAsync(new Paging(1, int.MaxValue), ct)).ToList();
}
public sealed class GetClientByIdHandler : IRequestHandler<GetClientByIdQuery, ClientReadModel?>, IQueryHandler
{
    private readonly IReadRepository<IClients, ClientReadModel> _read;
    public GetClientByIdHandler(IReadRepository<IClients, ClientReadModel> read) => _read = read;
    public Task<ClientReadModel?> Handle(GetClientByIdQuery r, CancellationToken ct) => _read.GetByIdAsync(r.AggregateId, ct);
}
public sealed class GetClientByPhoneHandler : IRequestHandler<GetClientByPhoneQuery, ClientReadModel?>, IQueryHandler
{
    private readonly IReadRepository<IClients, ClientReadModel> _read;
    public GetClientByPhoneHandler(IReadRepository<IClients, ClientReadModel> read) => _read = read;
    public async Task<ClientReadModel?> Handle(GetClientByPhoneQuery r, CancellationToken ct)
        => (await _read.GetAllAsync(new Paging(1, int.MaxValue), ct)).FirstOrDefault(x => x.Phone == r.Phone);
}
