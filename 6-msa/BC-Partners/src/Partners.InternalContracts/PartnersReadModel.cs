namespace Partners.InternalContracts;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public sealed class PartnersReadModel : IReadModel<Partners.Domain.IPartners>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Photo { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
    public DateTime? DeletedAt { get; set; }
}