using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;
using AppleHealthGateway.DomainServices;
using MediatR;

namespace AppleHealthGateway.Application.Commands;

public class UpsertImportStateHandler : IRequestHandler<UpsertImportStateCommand, AppleHealthGatewayOperationResult>
{
    private readonly IAppleImportAggregateProvider _provider;

    public UpsertImportStateHandler(IAppleImportAggregateProvider provider) => _provider = provider;

    public async Task<AppleHealthGatewayOperationResult> Handle(UpsertImportStateCommand request, CancellationToken ct)
    {
        var draft = new AppleImportDraft
        {
            SourceId = request.SourceId,
            Metrics = request.Metrics ?? new List<string>(),
            LastImport = request.LastImport,
            ProcessedFiles = request.ProcessedFiles ?? new List<string>()
        };
        var aggregateId = AppleImportModelFactory.ToGuid(request.SourceId);

        var error = AppleImportModelFactory.Validate(draft);
        if (error != null)
            return AppleHealthGatewayOperationResult.Failed(aggregateId, error);

        var aggregate = await _provider.GetAggregateAsync(aggregateId, ct);

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(),
            nameof(IAppleImportAggregate.UpsertImportState),
            "UpsertImportState",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var model = DynamicAnemicModel<IAppleHealthGateway>.Create(
            aggregateId, 0, Guid.NewGuid(),
            nameof(IAppleImportAggregate.UpsertImportState), "UpsertImportState",
            AppleImportModelFactory.BuildValueObjects(draft));

        var result = aggregate.UpsertImportState(model, command);
        return AppleHealthGatewayOperationResult.From(aggregateId, result);
    }
}
