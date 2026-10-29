using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Privacy.Domain;
using Privacy.Domain.Abstraction;

namespace Privacy.DomainServices;

/// <summary>
/// РџСЂРѕРІР°Р№РґРµСЂ Р°РіСЂРµРіР°С‚Р° PinCredential.
/// </summary>
public interface IPrivacyAggregateProvider : IAggregateProvider<IPrivacy, IPinCredentialAggregate> { }
