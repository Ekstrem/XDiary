namespace Privacy.Domain.Abstraction;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Privacy.Domain;

/// <summary>Агрегат Privacy: состояние (анемичная модель) + бизнес-операции.</summary>
public interface IPinCredentialAggregate : IAggregate<IPrivacy>, IPinCredentialAnemicModel
{
    IPinCredentialRoot Root { get; }
    bool Locked { get; }
    AggregateResult<IPrivacy, IPinCredentialAnemicModel> SetPin(Guid clientId, string salt, string hash, int iterations, CommandToAggregate? command = null);
    AggregateResult<IPrivacy, IPinCredentialAnemicModel> ChangePin(string salt, string hash, int iterations, CommandToAggregate? command = null);
    AggregateResult<IPrivacy, IPinCredentialAnemicModel> Remove(CommandToAggregate? command = null);
    AggregateResult<IPrivacy, IPinCredentialAnemicModel> Lock(CommandToAggregate? command = null);
    AggregateResult<IPrivacy, IPinCredentialAnemicModel> Unlock(CommandToAggregate? command = null);
}
