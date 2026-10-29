using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Privacy.Domain;

namespace Privacy.Domain.Tests;

/// <summary>
/// Строит анемичные модели PinCredential для тестов (аналог PinCredentialDraft).
/// </summary>
public static class PinCredentialBuilder
{
    public static Dictionary<string, IValueObject> BuildValueObjects(
        string userId = "user-1",
        string salt = "c2FsdA==",
        string hash = "AABBCCDD",
        int iterations = 100000)
    {
        return new Dictionary<string, IValueObject>
        {
            ["Root"] = new DynamicValueObject("PinCredentialRoot", new Dictionary<string, object?>
            {
                ["UserId"] = userId
            }),
            ["Salt"] = new DynamicValueObject("PinSalt", new Dictionary<string, object?>
            {
                ["Salt"] = salt
            }),
            ["Hash"] = new DynamicValueObject("PinHash", new Dictionary<string, object?>
            {
                ["Hash"] = hash
            }),
            ["Iterations"] = new DynamicValueObject("PinIterations", new Dictionary<string, object?>
            {
                ["Iterations"] = iterations
            })
        };
    }

    public static IAnemicModel<IPrivacy> NewCredentialModel(Guid id)
        => DynamicAnemicModel<IPrivacy>.Create(id, 0, Guid.NewGuid(), "SetPin", "SetPin", BuildValueObjects());

    public static IAnemicModel<IPrivacy> ExistingCredentialModel(Guid id)
        => DynamicAnemicModel<IPrivacy>.Create(id, 1, Guid.NewGuid(), "SetPin", "SetPin", BuildValueObjects());
}
