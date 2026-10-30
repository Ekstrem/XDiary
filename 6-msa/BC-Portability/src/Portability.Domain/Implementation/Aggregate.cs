namespace Portability.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using Portability.Domain;
using Portability.Domain.Abstraction;
using Portability.Domain.Specifications;
using XDiary.Contracts;

public sealed class Aggregate
{
    public ICsvDocumentAnemicModel Model { get; }
    private Aggregate(ICsvDocumentAnemicModel model) => Model = model;
    public static Aggregate CreateInstance(ICsvDocumentAnemicModel model) => new(model);

    private AggregateResult<IPortability, ICsvDocumentAnemicModel> Success(AnemicModel m)
    { var d = BusinessOperationData<IPortability, ICsvDocumentAnemicModel>.Commit<IPortability, ICsvDocumentAnemicModel>(Model, m); return new AggregateResultSuccess<IPortability, ICsvDocumentAnemicModel>(d); }
    private AggregateResult<IPortability, ICsvDocumentAnemicModel> Fail(string e)
    { var d = BusinessOperationData<IPortability, ICsvDocumentAnemicModel>.Commit<IPortability, ICsvDocumentAnemicModel>(Model, Model); return new AggregateResultException<IPortability, ICsvDocumentAnemicModel>(d, new FailedSpecification<IPortability, ICsvDocumentAnemicModel>(e)); }
    private AnemicModel Clone() => new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = Model.Root, Rows = Model.Rows, Kind = Model.Kind };

    private AggregateResult<IPortability, ICsvDocumentAnemicModel> Build(IReadOnlyList<string> headers, IReadOnlyList<string> rows, string kind)
    {
        var v = new HeaderContractValidator();
        if (!v.IsSatisfiedBy(headers)) return Fail(v.Reason);
        var m = Clone(); m.Root = CsvDocumentRoot.CreateInstance(headers); m.Rows = rows; m.Kind = kind; return Success(m);
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

    public AggregateResult<IPortability, ICsvDocumentAnemicModel> ExportRecords(IReadOnlyList<string> rows) => Build(CsvHeaderContract.All, rows, "records");
    public AggregateResult<IPortability, ICsvDocumentAnemicModel> ImportRecords(IReadOnlyList<string> rows) => Build(CsvHeaderContract.All, DedupById(rows), "records");
    public AggregateResult<IPortability, ICsvDocumentAnemicModel> ImportSomatic(IReadOnlyList<string> rows) => Build(CsvHeaderContract.All, DedupById(rows), "somatic");
}