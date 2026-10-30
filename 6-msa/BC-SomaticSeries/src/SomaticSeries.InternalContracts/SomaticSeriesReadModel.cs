namespace SomaticSeries.InternalContracts;
using XDiary.Contracts;
public sealed class SomaticSeriesReadModel : IReadModel<SomaticSeries.Domain.ISomaticSeries>
{
    public Guid Id { get; set; }
    public string T { get; set; } = string.Empty;
    public int Hr { get; set; }
    public int BodyBattery { get; set; }
}