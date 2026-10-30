namespace DailyMetrics.Storage;
using DailyMetrics.Domain.Abstraction;
using DailyMetrics.Domain.Implementation;
using DailyMetrics.InternalContracts;
public static class DailyMetricsMapper
{
    public static IDailyMetricsAnemicModel MapToModel(DailyMetricsEntity e)
    {
        var m = new AnemicModel { Id = e.Id, Version = e.Version };
        m.Root = DailyMetricsRoot.CreateInstance(e.Date);
        m.Stress = e.Stress; m.RestingHr = e.RestingHr; m.Hrv = e.Hrv;
        m.Sleep = Sleep.CreateInstance(e.SleepScore, e.SleepDurationMin, e.SleepDeepMin, e.SleepLightMin, e.SleepRemMin, e.SleepAwakeMin);
        return m;
    }
    public static void MapToEntity(IDailyMetricsAnemicModel m, DailyMetricsEntity e)
    { e.Id = m.Id; e.Version = m.Version; e.Date = m.Root.Date; e.Stress = m.Stress; e.RestingHr = m.RestingHr; e.Hrv = m.Hrv; e.SleepScore = m.Sleep.Score; e.SleepDurationMin = m.Sleep.DurationMin; e.SleepDeepMin = m.Sleep.DeepMin; e.SleepLightMin = m.Sleep.LightMin; e.SleepRemMin = m.Sleep.RemMin; e.SleepAwakeMin = m.Sleep.AwakeMin; }
    public static DailyMetricsReadModel MapToReadModel(DailyMetricsEntity e) => new DailyMetricsReadModel { Id = e.Id, Date = e.Date, Stress = e.Stress, RestingHr = e.RestingHr, Hrv = e.Hrv, SleepScore = e.SleepScore, SleepDurationMin = e.SleepDurationMin, SleepDeepMin = e.SleepDeepMin, SleepLightMin = e.SleepLightMin, SleepRemMin = e.SleepRemMin, SleepAwakeMin = e.SleepAwakeMin };
}