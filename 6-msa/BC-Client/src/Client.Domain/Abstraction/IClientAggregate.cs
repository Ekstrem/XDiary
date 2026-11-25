namespace Client.Domain.Abstraction;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Client.Domain;

/// <summary>Агрегат клиента: состояние (анемичная модель) + бизнес-операции.</summary>
public interface IClientAggregate : IAggregate<IClients>, IClientAnemicModel
{
    AggregateResult<IClients, IClientAnemicModel> Register(string phone, string otp, CommandToAggregate? command = null);
    AggregateResult<IClients, IClientAnemicModel> VerifyOtp(string code, CommandToAggregate? command = null);
    AggregateResult<IClients, IClientAnemicModel> RecordFailedAttempt(CommandToAggregate? command = null);
    AggregateResult<IClients, IClientAnemicModel> UpdateProfile(string displayName, string gender, string? photoUrl, CommandToAggregate? command = null);
    AggregateResult<IClients, IClientAnemicModel> ReissueOtp(string otp, CommandToAggregate? command = null);
}
