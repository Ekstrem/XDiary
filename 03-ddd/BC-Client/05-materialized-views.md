# Materialized Views: Клиент (Client)

## Проекция (ReadModel)
| Store | Тип | Обслуживает |
|---|---|---|
| `clients` | ReadModel (keyPath `id`) | ClientView |

## Store: clients
| Поле | Тип | Источник |
|---|---|---|
| id | uuid PK | фреймворк DF |
| phone | string | сущность |
| verified | bool | `PhoneVerified` |
| displayName | string | `ClientProfileCompleted` / `ClientProfileUpdated` |
| gender | `male\|female` | `ClientProfileCompleted` / `ClientProfileUpdated` |
| photo | string\|null | `ClientProfileUpdated` |

> `linkedPartnerId` **удалён из проекции** (ADR-024): «я» — не партнёр. События `PartnerLinked`/`PartnerUnlinked`
> больше не порождаются; строка профиля заполняется при `CompleteProfile`.

## Проекционные правила (событие → действие)
| Событие | Действие |
|---|---|
| `ClientRegistered` | INSERT |
| `PhoneVerified` | UPDATE (verified=true) |
| `ClientProfileCompleted` | UPDATE (displayName, gender, photo) |
| `ClientProfileUpdated` | UPDATE (displayName, gender, photo) |
