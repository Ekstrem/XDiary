namespace AppleHealthGateway.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;
using XDiary.Contracts;

public sealed class Aggregate
{
    public IAppleHealthGatewayAnemicModel Model { get; }
    private Aggregate(IAppleHealthGatewayAnemicModel model) => Model = model;
    public static Aggregate CreateInstance(IAppleHealthGatewayAnemicModel model) => new(model);

    private AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> Success(AnemicModel m)
    { var d = BusinessOperationData<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>.Commit<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>(Model, m); return new AggregateResultSuccess<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>(d); }
    private AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> Fail(string e)
    { var d = BusinessOperationData<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>.Commit<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>(Model, Model); return new AggregateResultException<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>(d, new FailedSpecification<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>(e)); }
    private AnemicModel Clone() => new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = Model.Root, ImportConfig = Model.ImportConfig, LastImport = Model.LastImport, ProcessedFiles = Model.ProcessedFiles };

    public AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> Configure(IEnumerable<string> metrics)
    {
        var m = Clone(); m.Root = AppleImportRoot.CreateInstance("apple"); m.ImportConfig = ImportConfig.CreateInstance(metrics); return Success(m);
    }
    public AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> ImportFile(string fileName)
    {
        if (Model.ProcessedFiles.Contains(fileName)) return Success(Clone()); // SkipDuplicate
        var files = new List<string>(Model.ProcessedFiles) { fileName };
        var m = Clone(); m.ProcessedFiles = files; m.LastImport = DateTimeOffset.UtcNow.ToString("o"); return Success(m);
    }
}