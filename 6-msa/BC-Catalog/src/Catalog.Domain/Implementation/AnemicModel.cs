namespace Catalog.Domain.Implementation;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Catalog.Domain.Abstraction;

// Канон EDM §8.1: иммутабельный агрегат — бизнес-метод порождает НОВУЮ версию,
// поэтому поля init-only, а не set (мутация через `with`, не через сеттеры).
internal sealed record AnemicModel : ICatalogAnemicModel
{
    // QG-012: фабрика начального (пустого) состояния с явным Id.
    public static ICatalogAnemicModel Create(Guid id) => new AnemicModel { Id = id };

    public Guid Id { get; init; } = Guid.NewGuid();
    public long Version { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string CommandName { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public Guid CorrelationToken { get; init; } = Guid.NewGuid();
    public ICatalogItemRoot Root { get; init; } = null!;
    public string Photo { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public bool Archived { get; init; }
    public string FantasyPartner { get; init; } = string.Empty;
    public Guid? ClientId { get; init; }

    public IDictionary<string, IValueObject> Invariants => GetValueObjects();

    public IDictionary<string, IValueObject> GetValueObjects() => new Dictionary<string, IValueObject>
    {
        ["Root"] = Root,
        ["ClientId"] = new ClientIdValueObject(ClientId),
        ["Photo"] = new StringValueObject(Photo),
        ["Notes"] = new StringValueObject(Notes),
        ["Brand"] = new StringValueObject(Brand),
        ["Archived"] = new BoolValueObject(Archived),
        ["FantasyPartner"] = new StringValueObject(FantasyPartner),
    };
}
