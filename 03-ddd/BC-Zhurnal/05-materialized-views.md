# Materialized Views: Журнал интимной активности

## Проекции (IndexedDB)
| Store | Тип | Обслуживает |
|---|---|---|
| `records` | Основная (keyPath `id`) | JournalView, RecordDetailView |

## Store: records
| Поле | Тип | Источник (RecordDraft → поле) |
|---|---|---|
| id | string PK | uuid / RecordDraft.id |
| startedAt | string | RecordDraft.startedAt |
| durationMin | int | RecordDraft.durationMin |
| partner | string | RecordDraft.partner |
| place | string | RecordDraft.place |
| positions | string[] | RecordDraft.positions |
| tags | string[] | RecordDraft.tags |
| costumes | string[] | RecordDraft.costumes |
| toys | string[] | RecordDraft.toys |
| fantasies | string[] | RecordDraft.fantasies |
| contraceptives | string[] | RecordDraft.contraceptives |
| contraceptiveCount | int | RecordDraft.contraceptiveCount |
| orgasmsMe / orgasmsPartner | int | RecordDraft.orgasmsMe / orgasmsPartner |
| orgasmMeType / orgasmPartnerType | string[] | RecordDraft.orgasmMeType / orgasmPartnerType |
| orgasmMeRating / orgasmPartnerRating | int | RecordDraft.*Rating |
| garmin | bool | RecordDraft.garmin |
| garminId | string | RecordDraft.garminId |
| createdAt / updatedAt | string | системное время |

## Проекционные правила
| Событие | Действие |
|---|---|
| RecordCreated | INSERT |
| RecordUpdated | PUT (upsert по id) |
| RecordDeleted | DELETE |
| RecordImported | PUT (upsert по id) |

## Индексы
- `startedAt` — сортировка журнала по убыванию.
