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
    private AnemicModel Clone() => new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = Model.Root, Rows = Model.Rows };

    private AggregateResult<IPortability, ICsvDocumentAnemicModel> Build(IReadOnlyList<string> headers, IReadOnlyList<string> rows)
    {
        var v = new HeaderContractValidator();
        if (!v.IsSatisfiedBy(headers)) return Fail(v.Reason);
        var m = Clone(); m.Root = CsvDocumentRoot.CreateInstance(headers); m.Rows = rows; return Success(m);
    }
    public AggregateResult<IPortability, ICsvDocumentAnemicModel> ExportRecords(IReadOnlyList<string> rows) => Build(CsvHeaderContract.All, rows);
    public AggregateResult<IPortability, ICsvDocumentAnemicModel> ImportRecords(IReadOnlyList<string> rows) => Build(CsvHeaderContract.All, rows);
    public AggregateResult<IPortability, ICsvDocumentAnemicModel> ImportSomatic(IReadOnlyList<string> rows) => Build(CsvHeaderContract.All, rows);
}