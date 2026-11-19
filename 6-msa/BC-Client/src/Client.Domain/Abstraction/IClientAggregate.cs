namespace Client.Domain.Abstraction;

using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Client.Domain;

/// <summary>Агрегат клиента: состояние (анемичная модель) + бизнес-операции.</summary>
public interface IClientAggregate : IAggregate<IClients>, IClientAnemicModel
{
    AggregateResult<IClients, IClientAnemicModel> Register(string phone, string otp);
    AggregateResult<IClients, IClientAnemicModel> VerifyOtp(string code);
    AggregateResult<IClients, IClientAnemicModel> RecordFailedAttempt();
    AggregateResult<IClients, IClientAnemicModel> UpdateProfile(string displayName, string gender, string? photoUrl);
    AggregateResult<IClients, IClientAnemicModel> ReissueOtp(string otp);
}
