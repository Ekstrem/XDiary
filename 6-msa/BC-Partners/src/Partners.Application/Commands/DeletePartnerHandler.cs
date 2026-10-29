using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.DomainServices;
using MediatR;

namespace Partners.Application.Commands;

public class DeletePartnerHandler : IRequestHandler<DeletePartnerCommand, PartnersOperationResult>
{
    private readonly IPartnersAggregateProvider _provider;

    public DeletePartnerHandler(IPartnersAggregateProvider provider) => _provider = provider;

    public async Task<PartnersOperationResult> Handle(DeletePartnerCommand request, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(request.AggregateId, ct);

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(),
            nameof(IPartnerAggregate.DeletePartner),
            "DeletePartner",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var model = DynamicAnemicModel<IPartners>.Create(
            request.AggregateId, 0, Guid.NewGuid(),
            nameof(IPartnerAggregate.DeletePartner), "DeletePartner",
            new Dictionary<string, IValueObject>());

        var result = aggregate.DeletePartner(model, command);
        return PartnersOperationResult.From(request.AggregateId, result);
    }
}
