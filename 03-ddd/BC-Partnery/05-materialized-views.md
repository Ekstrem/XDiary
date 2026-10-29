# Materialized Views: Партнёры

## Проекции (IndexedDB)
| Store | Тип | Обслуживает |
|---|---|---|
| `partners` | Основная (keyPath `id`) | PartnersView, AvatarView |

## Store: partners
| Поле | Тип | Источник |
|---|---|---|
| id | string PK | uuid |
| name | string | PartnerDraft.name |
| photo | string (dataURL) | PartnerDraft.photo |
| gender | string | PartnerDraft.gender |
| notes | string | PartnerDraft.notes |
| createdAt / updatedAt | string | системное время |

## Проекционные правила
| Событие | Действие |
|---|---|
| PartnerCreated | INSERT |
| PartnerUpdated | PUT (upsert по id) |
| PartnerDeleted | DELETE |
