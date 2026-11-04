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

> **`gender` — словарь, а не свободная строка.** Допустимые значения: `male`, `female`
> (справочник пола — общий для Журнала, Партнёров и Каталога, см. `00-context-map.md` §Shared Kernel).
> Контракт **валидирует** значение (400 на неизвестное), а не принимает любую строку:
> иначе аналитика по полу и автоподбор органов/типов оргазмов развалятся на «Other»/«other»/«м».
> **Решение владельца (2026-11-03 16:54): «обойдёмся двумя»** — значений ровно **два**, `other` не добавляем.
> Расширение словаря — только новым решением владельца, отдельным коммитом (канвас + контракт + тест).

**2.2. Состояние (`AnemicModel`) — атрибуты корня агрегата (AR-017).** Таблица сверена с **кодом** на 2026-11-14:
`IClientAnemicModel.cs` (7 свойств) + `ClientRoot.cs` (`Phone` — VO `Root`) + `AnemicModel.cs` (служебные поля DF).

| Атрибут | Тип в коде | Кард. | Обяз. | Задаётся | Инвариант | Источник (US/ADR) |
|---|---|---|---|---|---|---|
| `Root.Phone` | string (VO `ClientRoot`) | 1 | **да**, при `Register` | `Register(phone)` | непустой (`PhoneRequiredValidator`); уникальность — `DuplicatePhoneException` («phone_taken»), проверка на уровне хранилища | US-001, ADR-021 |
| `Verified` | bool | 1 | да (`false` по умолчанию) | `VerifyOtp(code)` | `false` → профиль не заполнить (только повторный OTP) | US-001 |
| `DisplayName` | string | 1 | да для активного профиля | `CompleteProfile`, `UpdateProfile` | непустое после трима; менять может только владелец (`sub`) | US-002 |
| `Gender` | string | 1 | да для активного профиля | `CompleteProfile`, `UpdateProfile` | **строго `male`/`female`** (решение владельца 2026-11-03 16:54), «Other»/«м» → 400 | US-002, `00-context-map.md` §Shared Kernel |
| `PhotoUrl` | string? | 0..1 | нет | `UpdateProfile` | `null` = «не задано»; отсутствие поля в запросе = «не менялось» (это разные вещи) | US-002 |
| `OtpCode` | string | 0..1 | нет | `Register`, `VerifyOtp` | хранится только на время проверки; не логируется | US-001 |
| `OtpExpiresAt` | DateTime? | 0..1 | нет | `Register` | истёк → код недействителен (иначе OTP бессрочный) | US-001, ADR-021 |
| `OtpAttempts` | int | 1 | да (`0`) | `VerifyOtp` | превышен лимит → отказ (защита от перебора) | ADR-021 |
| `Id`, `Version`, `CommandName`, `SubjectName`, `CorrelationToken` | служебные DF | 1 | да | фреймворк | `Version` монотонна; `SubjectName` = `sub` владельца (скоуп) | ADR-003, ADR-008 |

**2.2.2. Расхождения «канвас vs код» — зафиксированы, не замазаны (владелец: @BE):**

| Что описано в §3 | Что есть в коде | Следствие |
|---|---|---|
| `status` (`Registered`/`Verified`/`Active`/`Deleted`) | **нет поля `status`** — есть только `bool Verified` | переход в `Active` и «удалён» ничем не выражены: канвас описывает **4 состояния**, модель — **2** |
| `deletedAt` + инвариант `DeletedAtSet` | **поля нет** | удаление аккаунта (ADR-021) не реализовано в модели; `Remove` отсутствует в агрегате |
| `pin` (Privacy) | в `BC-Client` **нет**; PIN-логика живёт в `BC-Privacy` (`BC-Privacy/Privacy.Domain`) | канвас и код расходятся по **границе контекста**: ADR-024 относит Privacy к Client, сервис — отдельный (`6-msa/BC-Privacy`). Это вопрос распределения, а не «забыли поле» — решение @AR + @BE |
| `LinkedPartnerId` | **ещё 5 вхождений в коде** `BC-Client` | ADR-024 требует удалить; канвас уже без него — код отстаёт (долг, не архитектура) |

**2.2.1. Как атрибут попадает в агрегат через API (AR-016: ссылки на `operationId`, без пересказа контракта).**
Пути, коды ответов и тела запросов здесь **не дублируются** — единственный источник: `04-architecture/api-index.yaml`.
Таблица отвечает на «какой командой этот атрибут задаётся», а не «какие поля в JSON».

| Атрибут | `operationId` (запись) | Примечание |
|---|---|---|
| `phone` | `Client.Register` | создаёт клиента; уникальность — инвариант домена, 400 при нарушении |
| `phone` (подтверждение) | `Client.Verify` | OTP; признак `PhoneVerified` |
| `displayName`, `gender`, `photo` | `Client.UpdateProfile` | онбординг: имя + пол один раз; аватар опционально |
| — (чтение своего) | `Client.Me` | 404 = клиента нет (не «пустой» профиль) |
| — (чтение по id) | `Client.Get` | чужой `id` → 403 (владелец проверен в контроллере) |
| — (сессия) | `Client.Refresh` | обновление пары токенов по refresh |

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

> **Профиль «я» живёт здесь, а не в `Partner`** (ADR-024). Раньше было `LinkedPartnerId` — VO «партнёр-«я»»;
> **удалено**: «я» в акте — неявный автор записи (`participant="me"`), а не запись партнёра.
>
> PIN (опциональный локальный замок) — политика приватности **этого же контекста** (ADR-024: Privacy — часть `Client`;
> раньше был отдельный BC, см. `BC-Privacy/01-context-canvas.md`).
**2.2.1. Как атрибут попадает в агрегат через API (AR-016: ссылки на `operationId`, без пересказа контракта).**
Пути, коды ответов и тела запросов здесь **не дублируются** — единственный источник: `04-architecture/api-index.yaml`.
Таблица отвечает на «какой командой этот атрибут задаётся», а не «какие поля в JSON».

| Атрибут | `operationId` (запись) | Примечание |
|---|---|---|
| `phone` | `Client.Register` | создаёт клиента; уникальность — инвариант домена, 400 при нарушении |
| `phone` (подтверждение) | `Client.Verify` | OTP; признак `PhoneVerified` |
| `displayName`, `gender`, `photo` | `Client.UpdateProfile` | онбординг: имя + пол один раз; аватар опционально |
| — (чтение своего) | `Client.Me` | 404 = клиента нет (не «пустой» профиль) |
| — (чтение по id) | `Client.Get` | чужой `id` → 403 (владелец проверен в контроллере) |
| — (сессия) | `Client.Refresh` | обновление пары токенов по refresh |

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

> **Профиль «я» живёт здесь, а не в `Partner`** (ADR-024). Раньше было `LinkedPartnerId` — VO «партнёр-«я»»;
> **удалено**: «я» в акте — неявный автор записи (`participant="me"`), а не запись партнёра.
>
> PIN (опциональный локальный замок) — политика приватности **этого же контекста** (ADR-024: Privacy — часть `Client`;
> раньше был отдельный BC, см. `BC-Privacy/01-context-canvas.md`).
