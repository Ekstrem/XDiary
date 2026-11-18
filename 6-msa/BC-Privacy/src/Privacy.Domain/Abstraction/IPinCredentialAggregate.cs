namespace Privacy.Domain.Abstraction;

using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Privacy.Domain;

/// <summary>Агрегат Privacy: состояние (анемичная модель) + бизнес-операции.</summary>
public interface IPinCredentialAggregate : IAggregate<IPrivacy>, IPinCredentialAnemicModel
{
    IPinCredentialRoot Root { get; }
    bool Locked { get; }
    AggregateResult<IPrivacy, IPinCredentialAnemicModel> SetPin(Guid clientId, string salt, string hash, int iterations);
    AggregateResult<IPrivacy, IPinCredentialAnemicModel> ChangePin(string salt, string hash, int iterations);
    AggregateResult<IPrivacy, IPinCredentialAnemicModel> Remove();
    AggregateResult<IPrivacy, IPinCredentialAnemicModel> Lock();
    AggregateResult<IPrivacy, IPinCredentialAnemicModel> Unlock();
}
