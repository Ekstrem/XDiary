namespace Partners.DomainServices;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Partners.Domain;
using XDiary.Contracts;
public sealed class Notifier : INotifier<IPartners>
{
    public void Notify<TModel>(AggregateResult<IPartners, TModel> result) where TModel : IAnemicModel<IPartners> { }
}