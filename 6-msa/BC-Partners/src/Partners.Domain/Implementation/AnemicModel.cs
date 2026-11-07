namespace Partners.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Partners.Domain.Abstraction;
public sealed class AnemicModel : IPartnerAnemicModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long Version { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string CommandName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public Guid CorrelationToken { get; set; } = Guid.NewGuid();
    public IPartnerRoot Root { get; set; } = null!;
    public string Photo { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
    public DateTime? DeletedAt { get; set; }
    public IDictionary<string, IValueObject> Invariants => GetValueObjects();
    public IDictionary<string, IValueObject> GetValueObjects() => new Dictionary<string, IValueObject>
    {
        ["Root"] = Root,
        ["ClientId"] = new ClientIdValueObject(ClientId),
        ["DeletedAt"] = new DeletedAtValueObject(DeletedAt),
        // TE-102: Photo/Gender/Notes обязаны быть в VOs — иначе UpdateProfile даёт пустой DIFF
        // и проекция не видит поля профиля (а Name затирается пустым).
        ["Photo"] = new StringValueObject(Photo),
        ["Gender"] = new StringValueObject(Gender),
        ["Notes"] = new StringValueObject(Notes),
    };
}