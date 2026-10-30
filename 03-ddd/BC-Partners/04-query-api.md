# Query API: Партнёры (Partners)

> Read-сторона (CEQRS): только из ReadModel.

## Запросы (чтение)
| Запрос | Эндпоинт | Response | Источник |
|---|---|---|---|
| Список партнёров | `GET /api/v1/partners` | PartnersView | ReadModel `partners` |
| Партнёр | `GET /api/v1/partners/{id}` | PartnerView | ReadModel |
| Для выбора (select) | `GET /api/v1/partners?select=1` | имена | ReadModel |

## Response DTO
- PartnersView: карточки (аватар, имя, пол).
- PartnerView: name, photo, gender, notes.

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| UI | все | управление партнёрами |
| Журнал | имя, пол | выбор партнёра, типы оргазмов |
| Каталог | список | привязка фантазии к партнёру |
