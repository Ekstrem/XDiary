using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;
using GarminGateway.DomainServices;
using MediatR;

namespace GarminGateway.Application.Commands;

public class UpsertSyncStateHandler : IRequestHandler<UpsertSyncStateCommand, GarminGatewayOperationResult>
{
    private readonly IGarminSyncAggregateProvider _provider;

    public UpsertSyncStateHandler(IGarminSyncAggregateProvider provider) => _provider = provider;

    public async Task<GarminGatewayOperationResult> Handle(UpsertSyncStateCommand request, CancellationToken ct)
    {
        var draft = new GarminSyncDraft
        {
            SourceId = request.SourceId,
            ApiKey = request.ApiKey,
            OauthToken = request.OauthToken,
            TokenExpiry = request.TokenExpiry,
            Metrics = request.Metrics ?? new List<string>(),
            WindowDays = request.WindowDays,
            LastSync = request.LastSync
        };
        var aggregateId = GarminSyncModelFactory.ToGuid(request.SourceId);

        var error = GarminSyncModelFactory.Validate(draft);
        if (error != null)
            return GarminGatewayOperationResult.Failed(aggregateId, error);

        var aggregate = await _provider.GetAggregateAsync(aggregateId, ct);

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(),
            nameof(IGarminSyncAggregate.UpsertSyncState),
            "UpsertSyncState",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var model = DynamicAnemicModel<IGarminGateway>.Create(
            aggregateId, 0, Guid.NewGuid(),
            nameof(IGarminSyncAggregate.UpsertSyncState), "UpsertSyncState",
            GarminSyncModelFactory.BuildValueObjects(draft));

        var result = aggregate.UpsertSyncState(model, command);
        return GarminGatewayOperationResult.From(aggregateId, result);
    }
}
