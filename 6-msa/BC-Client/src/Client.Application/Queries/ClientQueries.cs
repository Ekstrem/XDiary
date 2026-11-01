namespace Client.Application.Queries;
using MediatR;
using Client.InternalContracts;
public sealed record ListClientsQuery : IRequest<IReadOnlyList<ClientReadModel>>;
public sealed record GetClientByIdQuery(Guid AggregateId) : IRequest<ClientReadModel?>;
public sealed record GetClientByPhoneQuery(string Phone) : IRequest<ClientReadModel?>;
public sealed class ListClientsHandler : IRequestHandler<ListClientsQuery, IReadOnlyList<ClientReadModel>>
{
    private readonly IClientQueryRepository _repo;
    public ListClientsHandler(IClientQueryRepository repo) => _repo = repo;
    public Task<IReadOnlyList<ClientReadModel>> Handle(ListClientsQuery r, CancellationToken ct) => _repo.ListAsync(ct);
}
public sealed class GetClientByIdHandler : IRequestHandler<GetClientByIdQuery, ClientReadModel?>
{
    private readonly IClientQueryRepository _repo;
    public GetClientByIdHandler(IClientQueryRepository repo) => _repo = repo;
    public Task<ClientReadModel?> Handle(GetClientByIdQuery r, CancellationToken ct) => _repo.GetByIdAsync(r.AggregateId, ct);
}
public sealed class GetClientByPhoneHandler : IRequestHandler<GetClientByPhoneQuery, ClientReadModel?>
{
    private readonly IClientQueryRepository _repo;
    public GetClientByPhoneHandler(IClientQueryRepository repo) => _repo = repo;
    public Task<ClientReadModel?> Handle(GetClientByPhoneQuery r, CancellationToken ct) => _repo.GetByPhoneAsync(r.Phone, ct);
}