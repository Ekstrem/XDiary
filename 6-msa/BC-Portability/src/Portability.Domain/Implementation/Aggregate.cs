namespace Portability.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Portability.Domain;
using Portability.Domain.Abstraction;
using Portability.Domain.Specifications;
using XDiary.Contracts;

internal sealed class Aggregate : ICsvDocumentAggregate
{
    public ICsvDocumentAnemicModel Model { get; }
    private Aggregate(ICsvDocumentAnemicModel model) => Model = model;
    public static Aggregate CreateInstance(ICsvDocumentAnemicModel model) => new(model);

    // IAggregate<TBC> / IAnemicModel<TBC>:
    public Guid Id => Model.Id;
    public long Version => Model.Version;
    public Guid CorrelationToken => Model.CorrelationToken;
    public string CommandName => Model.CommandName;
    public string SubjectName => Model.SubjectName;
    public IDictionary<string, IValueObject> Invariants => Model.Invariants;
    public IDictionary<string, IValueObject> GetValueObjects() => Model.GetValueObjects();

    // IPortabilityAnemicModel:
    public ICsvDocumentRoot Root => Model.Root;
    public IReadOnlyList<string> Rows => Model.Rows;
    public string Kind => Model.Kind;
    public Guid? ClientId => Model.ClientId;
    private AggregateResult<IPortability, ICsvDocumentAnemicModel> Success(AnemicModel m)
    { var d = BusinessOperationData<IPortability, ICsvDocumentAnemicModel>.Commit<IPortability, ICsvDocumentAnemicModel>(Model, m); return new AggregateResultSuccess<IPortability, ICsvDocumentAnemicModel>(d); }
    private AggregateResult<IPortability, ICsvDocumentAnemicModel> Fail(string e)
    { var d = BusinessOperationData<IPortability, ICsvDocumentAnemicModel>.Commit<IPortability, ICsvDocumentAnemicModel>(Model, Model); return new AggregateResultException<IPortability, ICsvDocumentAnemicModel>(d, new FailedSpecification<IPortability, ICsvDocumentAnemicModel>(e)); }
    private AnemicModel Clone() => new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = Model.Root, Rows = Model.Rows, Kind = Model.Kind, ClientId = Model.ClientId };

    private AggregateResult<IPortability, ICsvDocumentAnemicModel> Build(IReadOnlyList<string> headers, IReadOnlyList<string> rows, string kind, Guid clientId)
    {
        var v = new HeaderContractSpecification();
        if (!v.IsSatisfiedBy(headers)) return Fail(v.Reason);
        return Success(Clone() with { Root = CsvDocumentRoot.CreateInstance(headers), Rows = rows, Kind = kind, ClientId = clientId });
    }

    // Идемпотентность по id (первая колонка CSV): последняя строка с тем же id перезаписывает предыдущую
    private static IReadOnlyList<string> DedupById(IReadOnlyList<string> rows)
    {
        var order = new List<string>();
        var index = new Dictionary<string, int>();
        foreach (var row in rows)
        {
            var id = row.Split(',')[0].Trim();
            if (index.TryGetValue(id, out var i)) { order[i] = row; }
            else { index[id] = order.Count; order.Add(row); }
        }
        return order;
    }

    public AggregateResult<IPortability, ICsvDocumentAnemicModel> ExportRecords(IReadOnlyList<string> rows, Guid clientId) => Build(CsvHeaderContract.All, rows, "records", clientId);
    public AggregateResult<IPortability, ICsvDocumentAnemicModel> ImportRecords(IReadOnlyList<string> rows, Guid clientId) => Build(CsvHeaderContract.All, DedupById(rows), "records", clientId);
    public AggregateResult<IPortability, ICsvDocumentAnemicModel> ImportSomatic(IReadOnlyList<string> rows, Guid clientId) => Build(CsvHeaderContract.All, DedupById(rows), "somatic", clientId);
}