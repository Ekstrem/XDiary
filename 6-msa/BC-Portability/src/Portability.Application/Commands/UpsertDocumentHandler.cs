using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using Portability.Domain;
using Portability.Domain.Abstraction;
using Portability.DomainServices;
using MediatR;

namespace Portability.Application.Commands;

public class UpsertDocumentHandler : IRequestHandler<UpsertDocumentCommand, PortabilityOperationResult>
{
    private readonly ICsvDocumentAggregateProvider _provider;

    public UpsertDocumentHandler(ICsvDocumentAggregateProvider provider) => _provider = provider;

    public async Task<PortabilityOperationResult> Handle(UpsertDocumentCommand request, CancellationToken ct)
    {
        var draft = new CsvDocumentDraft
        {
            SourceId = request.SourceId,
            Headers = request.Headers ?? new List<string>(),
            Rows = request.Rows ?? new List<string>()
        };
        var aggregateId = CsvDocumentModelFactory.ToGuid(request.SourceId);

        var error = CsvDocumentModelFactory.Validate(draft);
        if (error != null)
            return PortabilityOperationResult.Failed(aggregateId, error);

        var aggregate = await _provider.GetAggregateAsync(aggregateId, ct);

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(),
            nameof(ICsvDocumentAggregate.UpsertDocument),
            "UpsertDocument",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var model = DynamicAnemicModel<IPortability>.Create(
            aggregateId, 0, Guid.NewGuid(),
            nameof(ICsvDocumentAggregate.UpsertDocument), "UpsertDocument",
            CsvDocumentModelFactory.BuildValueObjects(draft));

        var result = aggregate.UpsertDocument(model, command);
        return PortabilityOperationResult.From(aggregateId, result);
    }
}
