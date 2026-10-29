using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using Privacy.Application.Security;
using Privacy.Domain;
using Privacy.Domain.Abstraction;
using Privacy.DomainServices;
using MediatR;

namespace Privacy.Application.Commands;

public class SetPinHandler : IRequestHandler<SetPinCommand, PrivacyOperationResult>
{
    private readonly IPrivacyAggregateProvider _provider;

    public SetPinHandler(IPrivacyAggregateProvider provider) => _provider = provider;

    public async Task<PrivacyOperationResult> Handle(SetPinCommand request, CancellationToken ct)
    {
        if (!IsValidPin(request.Pin))
            return PrivacyOperationResult.Failed(Guid.Empty, "pin must be 4-8 digits.");

        var salt = PasswordHasher.GenerateSalt();
        var hash = PasswordHasher.Hash(request.Pin, salt, PasswordHasher.DefaultIterations);

        var draft = new PinCredentialDraft
        {
            UserId = request.UserId,
            Salt = salt,
            Hash = hash,
            Iterations = PasswordHasher.DefaultIterations
        };
        var aggregateId = PinCredentialModelFactory.ToGuid(request.UserId);

        var error = PinCredentialModelFactory.Validate(draft);
        if (error != null)
            return PrivacyOperationResult.Failed(aggregateId, error);

        var aggregate = await _provider.GetAggregateAsync(aggregateId, ct);

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(),
            nameof(IPinCredentialAggregate.SetPin),
            "SetPin",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var model = DynamicAnemicModel<IPrivacy>.Create(
            aggregateId, 0, Guid.NewGuid(),
            nameof(IPinCredentialAggregate.SetPin), "SetPin",
            PinCredentialModelFactory.BuildValueObjects(draft));

        var result = aggregate.SetPin(model, command);
        return PrivacyOperationResult.From(aggregateId, result);
    }

    private static bool IsValidPin(string pin)
        => !string.IsNullOrEmpty(pin) && pin.Length is >= 4 and <= 8 && pin.All(char.IsDigit);
}
