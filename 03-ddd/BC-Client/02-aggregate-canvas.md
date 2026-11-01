# Aggregate Design Canvas: Client (Клиент)

> Bounded Context: Клиент (Client) — идентификация/аутентификация пользователя (ADR-021)
> Агрегат: Client (пользователь)

## 2. Description
**Идентификатор агрегата (DF):** `<guid, long>` — технический, для быстрого поиска. НЕ входит в сущность.

**Сущность (Entity — корень агрегата):**
- **Client** — совокупность признаков, идентифицирующих пользователя: **`phone`** (номер телефона, уникальный).

**VO:** Phone (номер), OtpCode (код), LinkedPartnerId (партнёр-«я»).

## 3. State Transitions
```
(нет) → [Register] → Registered
Registered → [VerifyOtp] → Verified
Verified → [LinkPartner] → Linked
Linked → [UnlinkPartner] → Verified
```

## 6. Handled Commands (именованные методы)
| Метод | Входные VO |
|---|---|
| `Register(phone)` | Phone |
| `VerifyOtp(code)` | OtpCode |
| `LinkPartner(partnerId)` | PartnerId |
| `UnlinkPartner()` | — |

## 7. Created Events (каждый метод → событие)
| Метод | Событие |
|---|---|
| `Register` | `ClientRegistered` |
| `VerifyOtp` | `PhoneVerified` |
| `LinkPartner` | `PartnerLinked` |
| `UnlinkPartner` | `PartnerUnlinked` |

> PIN (опциональный локальный замок) остаётся в **Privacy** BC (ADR-008); Client — только идентичность (номер) + верификация (OTP) + связь с партнёром.
