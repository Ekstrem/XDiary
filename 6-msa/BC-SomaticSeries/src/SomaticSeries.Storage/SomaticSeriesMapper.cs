namespace SomaticSeries.Storage;
using SomaticSeries.Domain.Abstraction;
using SomaticSeries.Domain.Implementation;
using SomaticSeries.InternalContracts;
public static class SomaticSeriesMapper
{
    public static ISomaticSeriesAnemicModel MapToModel(SomaticPointEntity e)
    { var m = new AnemicModel { Id = e.Id, Version = e.Version, ClientId = e.ClientId }; m.Root = SomaticPointRoot.CreateInstance(e.T); m.Hr = e.Hr; m.BodyBattery = e.BodyBattery; return m; }
    public static void MapToEntity(ISomaticSeriesAnemicModel m, SomaticPointEntity e)
    { e.Id = m.Id; e.Version = m.Version; e.T = m.Root.T; e.Hr = m.Hr; e.BodyBattery = m.BodyBattery; e.ClientId = m.ClientId; }
    public static SomaticSeriesReadModel MapToReadModel(SomaticPointEntity e) => new SomaticSeriesReadModel { Id = e.Id, T = e.T, Hr = e.Hr, BodyBattery = e.BodyBattery, ClientId = e.ClientId };
}