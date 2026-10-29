using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

namespace Privacy.Domain.Abstraction;

/// <summary>
/// Агрегат PinCredential: хеш PIN (PBKDF2-SHA256) с солью.
/// </summary>
public interface IPinCredentialAggregate : IAggregate<IPrivacy>
{
    AggregateResult<IPrivacy, IAnemicModel<IPrivacy>> SetPin(IAnemicModel<IPrivacy> model, ICommandToAggregate commandMetadata);
    AggregateResult<IPrivacy, IAnemicModel<IPrivacy>> RemovePin(IAnemicModel<IPrivacy> model, ICommandToAggregate commandMetadata);
}
