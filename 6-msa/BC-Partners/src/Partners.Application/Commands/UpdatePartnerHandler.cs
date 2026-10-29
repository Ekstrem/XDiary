using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.DomainServices;
using MediatR;

namespace Partners.Application.Commands;

public class UpdatePartnerHandler : IRequestHandler<UpdatePartnerCommand, PartnersOperationResult>
{
    private readonly IPartnersAggregateProvider _provider;

    public UpdatePartnerHandler(IPartnersAggregateProvider provider) => _provider = provider;

    public async Task<PartnersOperationResult> Handle(UpdatePartnerCommand request, CancellationToken ct)
    {
        var draft = request.Draft ?? new PartnerDraft();

        var error = PartnerModelFactory.Validate(draft);
        if (error != null)
            return PartnersOperationResult.Failed(request.AggregateId, error);

        var aggregate = await _provider.GetAggregateAsync(request.AggregateId, ct);

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(),
            nameof(IPartnerAggregate.UpdatePartner),
            "UpdatePartner",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var model = DynamicAnemicModel<IPartners>.Create(
            request.AggregateId, 0, Guid.NewGuid(),
            nameof(IPartnerAggregate.UpdatePartner), "UpdatePartner",
            PartnerModelFactory.BuildValueObjects(draft));

        var result = aggregate.UpdatePartner(model, command);
        return PartnersOperationResult.From(request.AggregateId, result);
    }
}
