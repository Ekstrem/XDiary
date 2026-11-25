namespace AppleHealthGateway.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;
using AppleHealthGateway.Domain.Specifications;
using XDiary.Contracts;

internal sealed class Aggregate : IAppleHealthGatewayAggregate
{
    public IAppleHealthGatewayAnemicModel Model { get; }
    private Aggregate(IAppleHealthGatewayAnemicModel model) => Model = model;
    public static Aggregate CreateInstance(IAppleHealthGatewayAnemicModel model) => new(model);

    // IAggregate<TBC> / IAnemicModel<TBC>:
    public Guid Id => Model.Id;
    public long Version => Model.Version;
    public Guid CorrelationToken => Model.CorrelationToken;
    public string CommandName => Model.CommandName;
    public string SubjectName => Model.SubjectName;
    public IDictionary<string, IValueObject> Invariants => Model.Invariants;
    public IDictionary<string, IValueObject> GetValueObjects() => Model.GetValueObjects();

    // IAppleHealthGatewayAnemicModel:
    public IAppleImportRoot Root => Model.Root;
    public IImportConfig ImportConfig => Model.ImportConfig;
    public string LastImport => Model.LastImport;
    public IReadOnlyList<string> ProcessedFiles => Model.ProcessedFiles;
    private AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> Success(AnemicModel m, CommandToAggregate? command = null, [System.Runtime.CompilerServices.CallerMemberName] string commandName = "")
    {
        command ??= CommandToAggregate.Commit(m.CorrelationToken, commandName, m.SubjectName, Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Model.Version + 1));
        m = m with { Version = command.Version, CommandName = command.CommandName, SubjectName = command.SubjectName }; // BE-150: Command.Version = новая версия агрегата (timestamp), acted-on Version остаётся в DomainEvent.Version
        var d = BusinessOperationData<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>.Commit<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>(Model, m); return new AggregateResultSuccess<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>(d); }
    private AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> Fail(string e)
    { var d = BusinessOperationData<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>.Commit<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>(Model, Model); return new AggregateResultException<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>(d, new FailedSpecification<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>(e)); }
    private AnemicModel Clone() => new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = Model.Root, ImportConfig = Model.ImportConfig, LastImport = Model.LastImport, ProcessedFiles = Model.ProcessedFiles };

    public AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> Configure(Guid clientId, IEnumerable<string> metrics, CommandToAggregate? command = null)
    {
        var s = new SingleSourcePerClientSpecification();
        if (!s.IsSatisfiedBy(clientId, Model)) return Fail(s.Reason);
        return Success(Clone() with { Root = AppleImportRoot.CreateInstance(clientId, "apple"), ImportConfig = Implementation.ImportConfig.CreateInstance(metrics) }, command);
    }
    public AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> ImportFile(string fileName, CommandToAggregate? command = null)
    {
        if (Model.ProcessedFiles.Contains(fileName)) return Success(Clone(), command); // SkipDuplicate
        var files = new List<string>(Model.ProcessedFiles) { fileName };
        return Success(Clone() with { ProcessedFiles = files, LastImport = DateTimeOffset.UtcNow.ToString("o") }, command);
    }
}