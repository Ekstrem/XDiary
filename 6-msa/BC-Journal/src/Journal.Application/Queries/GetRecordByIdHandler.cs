using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Journal.Domain;
using Journal.InternalContracts;
using MediatR;

namespace Journal.Application.Queries;

public class GetRecordByIdHandler : IRequestHandler<GetRecordByIdQuery, JournalReadModel?>
{
    private readonly IReadRepository<IJournal, JournalReadModel> _repository;

    public GetRecordByIdHandler(IReadRepository<IJournal, JournalReadModel> repository) => _repository = repository;

    public Task<JournalReadModel?> Handle(GetRecordByIdQuery request, CancellationToken ct)
        => _repository.GetByIdAsync(request.AggregateId, ct);
}
