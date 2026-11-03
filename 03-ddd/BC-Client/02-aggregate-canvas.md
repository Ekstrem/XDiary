# Aggregate Design Canvas: Client (Клиент)

> Bounded Context: Клиент (Client) — идентификация/аутентификация пользователя (ADR-021)
> Агрегат: Client (пользователь)

## 2. Description
**Идентификатор агрегата (DF):** `<guid, long>` — технический, для быстрого поиска. НЕ входит в сущность.

**Сущность (Entity — корень агрегата):**
- **Client** — аккаунт пользователя:
  - **`phone`** (номер телефона, уникальный) — идентичность
  - **`displayName`** (string) — имя «я» (спрашиваем один раз на онбординге)
  - **`gender`** («male» | «female») — пол «я» (там же)

**VO:** Phone (номер), OtpCode (код), DisplayName (имя), Gender (пол), Photo (опционально).

## 3. State Transitions
```
(нет) → [Register] → Registered
Registered → [VerifyOtp] → Verified
Verified → [CompleteProfile(name, gender)] → Active
Active → [UpdateProfile(name, gender, photo?)] → Active
```

## 6. Handled Commands (именованные методы)
| Метод | Входные VO |
|---|---|
| `Register(phone)` | Phone |
| `VerifyOtp(code)` | OtpCode |
| `CompleteProfile(displayName, gender, photo?)` | DisplayName, Gender, Photo |
| `UpdateProfile(displayName, gender, photo?)` | DisplayName, Gender, Photo |

## 7. Created Events (каждый метод → событие)
| Метод | Событие |
|---|---|
| `Register` | `ClientRegistered` |
| `VerifyOtp` | `PhoneVerified` |
| `CompleteProfile` | `ClientProfileCompleted` |
| `UpdateProfile` | `ClientProfileUpdated` |

> **Профиль «я» живёт здесь, а не в `Partner`** (ADR-024). Раньше было `Client.LinkedPartnerId` — VO «партнёр-«я»»;
> **удалено**: «я» в акте — неявный автор записи (`participant="me"`), а не запись партнёра.
>
> PIN (опциональный локальный замок) — политика приватности **этого же контекста** (ADR-024: Privacy — часть `Client`;
> раньше был отдельный BC, см. `BC-Privacy/01-context-canvas.md`).
