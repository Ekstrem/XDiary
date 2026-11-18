namespace AppleHealthGateway.Application.Commands;
using AppleHealthGateway.Application;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using MediatR;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;
using AppleHealthGateway.InternalContracts;
using XDiary.Contracts;

public sealed class ConfigureHandler : IRequestHandler<ConfigureCommand, AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IAppleHealthGateway, IAppleHealthGatewayAggregate> _provider;
    private readonly IObserver<AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>> _observer;
    private readonly IActorProvider _actor;
    private readonly IReadRepository<IAppleHealthGateway, AppleImportReadModel> _read;
    public ConfigureHandler(IAggregateProvider<IAppleHealthGateway, IAppleHealthGatewayAggregate> provider, IObserver<AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>> observer, IActorProvider actor, IReadRepository<IAppleHealthGateway, AppleImportReadModel> read) { _provider = provider; _observer = observer; _actor = actor; _read = read; }
    public async Task<AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>> Handle(ConfigureCommand r, CancellationToken ct)
    {
        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        var existing = all.FirstOrDefault(x => x.ClientId == _actor.ClientId && x.SourceId == "apple");
        var aggregate = await _provider.GetAggregateAsync(existing?.Id ?? Guid.NewGuid(), ct);
        ((IObservable<AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>>)aggregate).Subscribe(_observer);
        return aggregate.Configure(_actor.ClientId, r.Metrics);
    }
}
public sealed class ImportFileHandler : IRequestHandler<ImportFileCommand, AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IAppleHealthGateway, IAppleHealthGatewayAggregate> _provider;
    private readonly IObserver<AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>> _observer;
    private readonly IActorProvider _actor;
    private readonly IReadRepository<IAppleHealthGateway, AppleImportReadModel> _read;
    public ImportFileHandler(IAggregateProvider<IAppleHealthGateway, IAppleHealthGatewayAggregate> provider, IObserver<AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>> observer, IActorProvider actor, IReadRepository<IAppleHealthGateway, AppleImportReadModel> read) { _provider = provider; _observer = observer; _actor = actor; _read = read; }
    public async Task<AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>> Handle(ImportFileCommand r, CancellationToken ct)
    {
        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        var existing = all.FirstOrDefault(x => x.ClientId == _actor.ClientId && x.SourceId == "apple");
        var aggregate = await _provider.GetAggregateAsync(existing?.Id ?? Guid.NewGuid(), ct);
        ((IObservable<AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>>)aggregate).Subscribe(_observer);
        return aggregate.ImportFile(r.FileName);
    }
}
