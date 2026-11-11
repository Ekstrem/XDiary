# Materialized Views: Партнёры (Partners)

## Проекция (ReadModel)
| Store | Тип | Обслуживает |
|---|---|---|
| `partners` | ReadModel (keyPath `id`) | PartnersView |

## Store: partners
| Поле | Тип | Источник |
|---|---|---|
| id | uuid PK | фреймворк DF (`<guid, long>`) |
| name | string | сущность |
| photo | string (dataURL) | VO |
| gender | string | VO |
| notes | string | VO |
| createdAt / updatedAt | string | системное |

## Проекционные правила (событие → действие)
| Событие | Действие |
|---|---|
| `PartnerRegistered` | INSERT |
| `PartnerRenamed` | PUT (name) |
| `PartnerProfileUpdated` | PUT (photo/gender/notes) |
| `PartnerRemoved` | DELETE |

## Индексы
- `name` — выбор по имени.

## Физические таблицы: что реально создаётся (сверено 2026-11-10, @AR)

| Логический store (выше) | Физическая таблица | Где создаётся |
|---|---|---|
| `partners` (command-side) | **`partners`** | `Partners.Storage/Migrations/20261030112254_InitialCreate.cs` |
| `partners` (read-store, его читают запросы) | **`partners_read`** | `Migrations/ReadStore/20261105170447_AddReadStores.cs` |
| — (исходящие события) | **`outbox`** | `Migrations/20261105165235_AddOutbox.cs` |

То есть имя `partners` носят **две** таблицы (write и read), и это стоит держать в голове при разборе «почему в списке не то, что в БД».

## Эталон сверки «поля read-модели ⊆ колонки DDL» (2026-11-10, @AR; вход для `DO-109`)

| Таблица | Полей модели | Колонок DDL | Расхождений |
|---|---|---|---|
| `partners_read` | **7** | **7** | 0 |

Суммарно по двум BC: **4 модели с DDL**, **40 полей**, **0 расхождений** — это и есть ожидаемая квитанция
`check_readstore_ddl` после починки фильтра (`сверено моделей с DDL: 4`). Сейчас гейт даёт `0` моделей и `rc=2`
(«ни одной модели с DDL не найдено») — карточка `DO-109`.

