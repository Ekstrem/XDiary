using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.DomainServices;
using MediatR;

namespace Partners.Application.Commands;

public class CreatePartnerHandler : IRequestHandler<CreatePartnerCommand, PartnersOperationResult>
{
    private readonly IPartnersAggregateProvider _provider;

    public CreatePartnerHandler(IPartnersAggregateProvider provider) => _provider = provider;

    public async Task<PartnersOperationResult> Handle(CreatePartnerCommand request, CancellationToken ct)
    {
        var draft = request.Draft ?? new PartnerDraft();
        var aggregateId = Guid.NewGuid();

        var error = PartnerModelFactory.Validate(draft);
        if (error != null)
            return PartnersOperationResult.Failed(aggregateId, error);

        var aggregate = await _provider.GetAggregateAsync(aggregateId, ct);
        var model = DynamicAnemicModel<IPartners>.Create(
            aggregateId, 0, Guid.NewGuid(),
            nameof(IPartnerAggregate.CreatePartner), "CreatePartner",
            PartnerModelFactory.BuildValueObjects(draft));

        var result = aggregate.CreatePartner(model);
        return PartnersOperationResult.From(aggregateId, result);
    }
}
