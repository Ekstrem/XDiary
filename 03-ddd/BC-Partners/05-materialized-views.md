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
