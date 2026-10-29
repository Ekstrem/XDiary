using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Privacy.Domain;
using Privacy.Domain.Abstraction;
using Privacy.DomainServices;
using MediatR;

namespace Privacy.Application.Commands;

public class RemovePinHandler : IRequestHandler<RemovePinCommand, PrivacyOperationResult>
{
    private readonly IPrivacyAggregateProvider _provider;

    public RemovePinHandler(IPrivacyAggregateProvider provider) => _provider = provider;

    public async Task<PrivacyOperationResult> Handle(RemovePinCommand request, CancellationToken ct)
    {
        var aggregateId = PinCredentialModelFactory.ToGuid(request.UserId);
        var aggregate = await _provider.GetAggregateAsync(aggregateId, ct);

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(),
            nameof(IPinCredentialAggregate.RemovePin),
            "RemovePin",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var model = DynamicAnemicModel<IPrivacy>.Create(
            aggregateId, 0, Guid.NewGuid(),
            nameof(IPinCredentialAggregate.RemovePin), "RemovePin",
            new Dictionary<string, IValueObject>
            {
                ["Root"] = new DynamicValueObject("PinCredentialRoot", new Dictionary<string, object?> { ["UserId"] = request.UserId })
            });

        var result = aggregate.RemovePin(model, command);
        return PrivacyOperationResult.From(aggregateId, result);
    }
}
