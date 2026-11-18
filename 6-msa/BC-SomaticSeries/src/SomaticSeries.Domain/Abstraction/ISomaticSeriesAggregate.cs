namespace SomaticSeries.Domain.Abstraction;

using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using SomaticSeries.Domain;

/// <summary>Агрегат SomaticSeries: состояние (анемичная модель) + бизнес-операции.</summary>
public interface ISomaticSeriesAggregate : IAggregate<ISomaticSeries>, ISomaticSeriesAnemicModel
{
    ISomaticPointRoot Root { get; }
    int Hr { get; }
    int BodyBattery { get; }
    Guid? ClientId { get; }
    AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel> RecordPoint(string t, int hr, int bodyBattery, Guid clientId);
}
