using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

namespace Privacy.Application.Commands;

/// <summary>
/// Строит Value Objects динамической анемичной модели PinCredential из PinCredentialDraft.
/// </summary>
internal static class PinCredentialModelFactory
{
    public static Dictionary<string, IValueObject> BuildValueObjects(PinCredentialDraft draft) => new()
    {
        ["Root"] = new DynamicValueObject("PinCredentialRoot", new Dictionary<string, object?>
        {
            ["UserId"] = draft.UserId
        }),
        ["Salt"] = new DynamicValueObject("PinSalt", new Dictionary<string, object?>
        {
            ["Salt"] = draft.Salt
        }),
        ["Hash"] = new DynamicValueObject("PinHash", new Dictionary<string, object?>
        {
            ["Hash"] = draft.Hash
        }),
        ["Iterations"] = new DynamicValueObject("PinIterations", new Dictionary<string, object?>
        {
            ["Iterations"] = draft.Iterations
        })
    };

    /// <summary>Детерминированный Guid из UserId (один PIN на пользователя).</summary>
    public static Guid ToGuid(string userId)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        return new Guid(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(userId)));
    }

    /// <summary>Инварианты, которые нельзя выразить DSL-метаданных.</summary>
    public static string? Validate(PinCredentialDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.UserId))
            return "userId is required.";
        if (string.IsNullOrWhiteSpace(draft.Salt) || string.IsNullOrWhiteSpace(draft.Hash))
            return "salt and hash are required.";
        return null;
    }
}
