namespace Partners.Domain.Abstraction;

using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Partners.Domain;

/// <summary>
/// Интерфейс агрегата партнёра (канон EDM): агрегат = состояние (анемичная модель) + бизнес-операции.
/// Живёт в Abstraction/ — реализация в Implementation/.
/// </summary>
public interface IPartnerAggregate : IAggregate<IPartners>, IPartnerAnemicModel
{
    AggregateResult<IPartners, IPartnerAnemicModel> Register(string name, Guid clientId);
    AggregateResult<IPartners, IPartnerAnemicModel> Rename(string name);
    AggregateResult<IPartners, IPartnerAnemicModel> UpdateProfile(string photo, string gender, string notes);
    AggregateResult<IPartners, IPartnerAnemicModel> LinkClient(Guid clientId);
    AggregateResult<IPartners, IPartnerAnemicModel> UnlinkClient();
    AggregateResult<IPartners, IPartnerAnemicModel> Remove();
}
