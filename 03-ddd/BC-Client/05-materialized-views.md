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
| linkedPartnerId | uuid\|null | `PartnerLinked` / `PartnerUnlinked` |

## Проекционные правила (событие → действие)
| Событие | Действие |
|---|---|
| `ClientRegistered` | INSERT |
| `PhoneVerified` | UPDATE (verified=true) |
| `PartnerLinked` | UPDATE (linkedPartnerId) |
| `PartnerUnlinked` | UPDATE (linkedPartnerId=null) |
