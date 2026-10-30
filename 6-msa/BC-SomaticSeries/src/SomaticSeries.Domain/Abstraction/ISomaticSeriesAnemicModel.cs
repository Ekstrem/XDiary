namespace SomaticSeries.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface ISomaticSeriesAnemicModel : IAnemicModel<ISomaticSeries>
{
    ISomaticPointRoot Root { get; }
    int Hr { get; }
    int BodyBattery { get; }
}