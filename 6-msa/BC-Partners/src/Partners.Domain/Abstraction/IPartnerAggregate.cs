using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

namespace Partners.Domain.Abstraction;

/// <summary>
/// Агрегат Partner (партнёр) контекста «Партнёры».
/// Методы делегируют в Aggregate&lt;IPartners&gt;.Operations из AbstractAggregate.
/// </summary>
public interface IPartnerAggregate : IAggregate<IPartners>
{
    /// <summary>Создать профиль партнёра.</summary>
    AggregateResult<IPartners, IAnemicModel<IPartners>> CreatePartner(IAnemicModel<IPartners> model);

    /// <summary>Отредактировать профиль партнёра.</summary>
    AggregateResult<IPartners, IAnemicModel<IPartners>> UpdatePartner(IAnemicModel<IPartners> model, ICommandToAggregate commandMetadata);

    /// <summary>Удалить партнёра.</summary>
    AggregateResult<IPartners, IAnemicModel<IPartners>> DeletePartner(IAnemicModel<IPartners> model, ICommandToAggregate commandMetadata);
}
