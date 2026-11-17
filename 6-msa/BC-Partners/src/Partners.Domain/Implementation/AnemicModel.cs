namespace Partners.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Partners.Domain.Abstraction;
// РљР°РЅРѕРЅ EDM: РёРјРјСѓС‚Р°Р±РµР»СЊРЅР°СЏ Р°РЅРµРјРёС‡РЅР°СЏ РјРѕРґРµР»СЊ вЂ” init-only, РјСѓС‚Р°С†РёСЏ С‡РµСЂРµР· `with`.
internal sealed record AnemicModel : IPartnerAnemicModel
{
    // QG-012: С„Р°Р±СЂРёРєР° РЅР°С‡Р°Р»СЊРЅРѕРіРѕ (РїСѓСЃС‚РѕРіРѕ) СЃРѕСЃС‚РѕСЏРЅРёСЏ СЃ СЏРІРЅС‹Рј Id.
    public static IPartnerAnemicModel Create(Guid id) => new AnemicModel { Id = id };

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
        // TE-102: Photo/Gender/Notes РѕР±СЏР·Р°РЅС‹ Р±С‹С‚СЊ РІ VOs вЂ” РёРЅР°С‡Рµ UpdateProfile РґР°С‘С‚ РїСѓСЃС‚РѕР№ DIFF
        // Рё РїСЂРѕРµРєС†РёСЏ РЅРµ РІРёРґРёС‚ РїРѕР»СЏ РїСЂРѕС„РёР»СЏ (Р° Name Р·Р°С‚РёСЂР°РµС‚СЃСЏ РїСѓСЃС‚С‹Рј).
        ["Photo"] = new StringValueObject(Photo),
        ["Gender"] = new StringValueObject(Gender),
        ["Notes"] = new StringValueObject(Notes),
    };
}
