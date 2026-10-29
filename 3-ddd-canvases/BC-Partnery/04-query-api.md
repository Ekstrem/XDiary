# Query API: Партнёры

## Запросы (чтение)
| Запрос | Функция (код) | Response | Источник |
|---|---|---|---|
| Список партнёров | renderPartners() | PartnersView | `partners` |
| Аватар | avatarHtml(p) | AvatarView (img/initial) | `partners.photo` |
| Форма редактирования | openPartnerEdit(id) | PartnerDraft | `partners` |
| Список для select | populateCatalogPartnerSelect() | имена | `partners` |

## Response DTO
- PartnersView: карточки (аватар, имя, пол).
- AvatarView: `<img>` или инициал, если фото нет.

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| UI пользователя | все | управление партнёрами |
| Журнал | avatarHtml, имя, пол | выбор партнёра и типы оргазмов |
| Каталог | populateCatalogPartnerSelect | привязка фантазии к партнёру |
