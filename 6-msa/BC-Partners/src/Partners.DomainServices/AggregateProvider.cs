namespace Partners.DomainServices;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.Domain.Implementation;

/// <summary>
/// QG-101/102/109: провайдер агрегата. Инжектит библиотечный IAnemicModelRepository (QG-109),
/// при промахе собирает агрегат из DefaultAnemicModel.Create (QG-102) и декорирует его Notifier'ом (QG-101).
/// </summary>
public sealed class AggregateProvider : IAggregateProvider<IPartners, IPartnerAggregate>
{
    private readonly IAnemicModelRepository<IPartners, IPartnerAnemicModel> _repository;

    public AggregateProvider(IAnemicModelRepository<IPartners, IPartnerAnemicModel> repository) => _repository = repository;

    public async Task<IPartnerAggregate> GetAggregateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var models = await _repository.GetById(id, cancellationToken);
        var model = models.FirstOrDefault() ?? DefaultAnemicModel.Create(id);
        return Notifier.Create(Aggregate.CreateInstance(model));
    }

    public async Task<IPartnerAggregate> GetAggregateAsync(Guid id, long version, CancellationToken cancellationToken = default)
    {
        var model = await _repository.GetByIdAndVersion(id, version, cancellationToken);
        return Notifier.Create(Aggregate.CreateInstance(model));
    }

    public IPartnerAggregate GetAggregate(Guid id, long version)
        => GetAggregateAsync(id, version, CancellationToken.None).GetAwaiter().GetResult();

    public IPartnerAggregate GetAggregate(Guid id)
        => GetAggregateAsync(id, CancellationToken.None).GetAwaiter().GetResult();
}
