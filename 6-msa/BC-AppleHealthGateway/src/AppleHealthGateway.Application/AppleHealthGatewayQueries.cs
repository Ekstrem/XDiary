namespace AppleHealthGateway.Application.Queries;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using MediatR;
using AppleHealthGateway.Domain;
using AppleHealthGateway.InternalContracts;
public sealed record GetImportStateQuery(Guid ClientId) : IRequest<AppleImportReadModel?>;
public sealed class GetImportStateHandler : IRequestHandler<GetImportStateQuery, AppleImportReadModel?>, IQueryHandler
{
    private readonly IReadRepository<IAppleHealthGateway, AppleImportReadModel> _read;
    public GetImportStateHandler(IReadRepository<IAppleHealthGateway, AppleImportReadModel> read) => _read = read;
    public async Task<AppleImportReadModel?> Handle(GetImportStateQuery r, CancellationToken ct)
        => (await _read.GetAllAsync(new Paging(1, int.MaxValue), ct)).FirstOrDefault(x => x.ClientId == r.ClientId && x.SourceId == "apple");
}
