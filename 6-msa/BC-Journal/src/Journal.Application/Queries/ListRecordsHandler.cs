using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Journal.Domain;
using Journal.InternalContracts;
using MediatR;

namespace Journal.Application.Queries;

public class ListRecordsHandler : IRequestHandler<ListRecordsQuery, List<JournalReadModel>>
{
    private readonly IReadRepository<IJournal, JournalReadModel> _repository;

    public ListRecordsHandler(IReadRepository<IJournal, JournalReadModel> repository) => _repository = repository;

    public async Task<List<JournalReadModel>> Handle(ListRecordsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 10000);

        var items = await _repository.GetAllAsync(new Paging(page, size), ct);
        return items.OrderByDescending(r => r.StartedAt).ToList();
    }
}
