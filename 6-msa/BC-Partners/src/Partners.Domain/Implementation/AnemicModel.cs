namespace Partners.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Partners.Domain.Abstraction;
// Канон EDM: иммутабельная анемичная модель — init-only, мутация через `with`.
public sealed record AnemicModel : IPartnerAnemicModel
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public long Version { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string CommandName { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public Guid CorrelationToken { get; init; } = Guid.NewGuid();
    public IPartnerRoot Root { get; init; } = null!;
    public string Photo { get; init; } = string.Empty;
    public string Gender { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public Guid? ClientId { get; init; }
    public DateTime? DeletedAt { get; init; }
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
