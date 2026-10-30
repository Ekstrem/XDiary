namespace AppleHealthGateway.Application.Commands;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;
using AppleHealthGateway.Domain.Implementation;
using AppleHealthGateway.InternalContracts;
using XDiary.Contracts;

public sealed class ConfigureHandler : IRequestHandler<ConfigureCommand, AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>>
{
    private readonly IAppleHealthGatewayRepository _repo;
    public ConfigureHandler(IAppleHealthGatewayRepository repo) => _repo = repo;
    public async Task<AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>> Handle(ConfigureCommand r, CancellationToken ct)
    { var m = await _repo.GetBySourceIdAsync("apple", ct) ?? new AnemicModel(); var res = Aggregate.CreateInstance(m).Configure(r.Metrics); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}
public sealed class ImportFileHandler : IRequestHandler<ImportFileCommand, AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>>
{
    private readonly IAppleHealthGatewayRepository _repo;
    public ImportFileHandler(IAppleHealthGatewayRepository repo) => _repo = repo;
    public async Task<AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>> Handle(ImportFileCommand r, CancellationToken ct)
    { var m = await _repo.GetBySourceIdAsync("apple", ct) ?? new AnemicModel(); var res = Aggregate.CreateInstance(m).ImportFile(r.FileName); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}