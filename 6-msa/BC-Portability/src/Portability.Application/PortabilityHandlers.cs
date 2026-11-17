namespace Portability.Application.Commands;
using Portability.Application;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using MediatR;
using Portability.Domain;
using Portability.Domain.Abstraction;
using XDiary.Contracts;

public sealed class ExportRecordsHandler : IRequestHandler<ExportRecordsCommand, AggregateResult<IPortability, ICsvDocumentAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IPortability, ICsvDocumentAggregate> _provider;
    private readonly IObserver<AggregateResult<IPortability, ICsvDocumentAnemicModel>> _observer;
    private readonly IActorProvider _actor;
    public ExportRecordsHandler(IAggregateProvider<IPortability, ICsvDocumentAggregate> provider, IObserver<AggregateResult<IPortability, ICsvDocumentAnemicModel>> observer, IActorProvider actor) { _provider = provider; _observer = observer; _actor = actor; }
    public async Task<AggregateResult<IPortability, ICsvDocumentAnemicModel>> Handle(ExportRecordsCommand r, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(Guid.NewGuid(), ct);
        ((IObservable<AggregateResult<IPortability, ICsvDocumentAnemicModel>>)aggregate).Subscribe(_observer);
        return aggregate.ExportRecords(r.Rows, _actor.ClientId);
    }
}
public sealed class ImportRecordsHandler : IRequestHandler<ImportRecordsCommand, AggregateResult<IPortability, ICsvDocumentAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IPortability, ICsvDocumentAggregate> _provider;
    private readonly IObserver<AggregateResult<IPortability, ICsvDocumentAnemicModel>> _observer;
    private readonly IActorProvider _actor;
    public ImportRecordsHandler(IAggregateProvider<IPortability, ICsvDocumentAggregate> provider, IObserver<AggregateResult<IPortability, ICsvDocumentAnemicModel>> observer, IActorProvider actor) { _provider = provider; _observer = observer; _actor = actor; }
    public async Task<AggregateResult<IPortability, ICsvDocumentAnemicModel>> Handle(ImportRecordsCommand r, CancellationToken ct)
    {
        var id = r.Id == Guid.Empty ? Guid.NewGuid() : r.Id;
        var aggregate = await _provider.GetAggregateAsync(id, ct);
        ((IObservable<AggregateResult<IPortability, ICsvDocumentAnemicModel>>)aggregate).Subscribe(_observer);
        return aggregate.ImportRecords(r.Rows, _actor.ClientId);
    }
}
public sealed class ImportSomaticHandler : IRequestHandler<ImportSomaticCommand, AggregateResult<IPortability, ICsvDocumentAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IPortability, ICsvDocumentAggregate> _provider;
    private readonly IObserver<AggregateResult<IPortability, ICsvDocumentAnemicModel>> _observer;
    private readonly IActorProvider _actor;
    public ImportSomaticHandler(IAggregateProvider<IPortability, ICsvDocumentAggregate> provider, IObserver<AggregateResult<IPortability, ICsvDocumentAnemicModel>> observer, IActorProvider actor) { _provider = provider; _observer = observer; _actor = actor; }
    public async Task<AggregateResult<IPortability, ICsvDocumentAnemicModel>> Handle(ImportSomaticCommand r, CancellationToken ct)
    {
        var id = r.Id == Guid.Empty ? Guid.NewGuid() : r.Id;
        var aggregate = await _provider.GetAggregateAsync(id, ct);
        ((IObservable<AggregateResult<IPortability, ICsvDocumentAnemicModel>>)aggregate).Subscribe(_observer);
        return aggregate.ImportSomatic(r.Rows, _actor.ClientId);
    }
}
