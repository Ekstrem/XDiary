# Патч `be131-stale-skip.patch` — BE-131 (soft-delete не доезжает до read-модели)

**Автор:** @TL (координатор), 2026-11-18 20:58
**Передан:** `→ @BE` (зона `6-msa/BC-Partners`), действует правило §1 `AGENTS.md`: если не применён за **1 час** от передачи — координатор применяет сам с пометкой «применено координатором, ответственность за закрытие — @TL».
**Проверено:** `git apply --check` → rc=0.

## Зачем

`smoke-api` красный (`PartnerSoftDeleteTests` 3 из 4), `deploy-prod` заблокирован. Замер @TL на dev (`single-9acec45`, 20:51):

```
create        : 200 f8381cbf-…
POST /remove  : 200 {"version":2,…}          ← событие пишется
list after    : 200 count = 1 | contains removed: true   ← НЕ исчез
GET removed   : 200 {"deletedAt":null}
```

## Корень

В `PartnerEventHandler.HandleAsync` (`Partners.Api/Events/PartnerEventConsumer.cs`) стоит stale-skip:

```csharp
if (existing.Version >= domainEvent.Version) return;   // QG-305
```

Он сравнивает **несравнимое**:
- у legacy-строк `PartnersReadModel.Version` содержит **ms-таймстамп** (`AnemicModel.Version` по умолчанию = `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`, ~1.79e12);
- у нового события `Version` — **малое число** (1, 2, 3 — после фикса `BE-130`/`BE-129` инкремент).

⇒ `existing.Version >= domainEvent.Version` **всегда истинно** для legacy-строк, и любое обновление (в т.ч. `Remove`) молча отбрасывается. Отсюда: регистрация работает (строки нет — ветка `else`), а удаление/`link-client` — нет.

## Что делает патч

Сравнивает только сопоставимые значения: ms-таймстамп считается legacy-значением и пропускается к записи.

```csharp
const long LegacyTimestampFloor = 1_000_000_000_000L;
if (existing.Version >= domainEvent.Version && existing.Version < LegacyTimestampFloor) return;
```

## Критерий приёмки

1. `POST /partners/{id}/remove` → 200; через ≤2 с `GET /partners` **не содержит** id;
2. `GET /partners/{id}` → 404 или 403;
3. повторный `remove` → 200, без `23505`;
4. `smoke-api`: `PartnerSoftDeleteTests` 4/4.

## Долгосрочно (не в этом патче)

Семантика `Version` в Partners расщеплена: таймстамп (legacy `AnemicModel` default) и инкремент (новые события). Нужно одно решение — либо везде инкремент (и бэкфилл существующих строк), либо везде монотонный timestamp. Это архитектурный вопрос к @BE/@AR, карточка `BE-131` содержит ссылку на этот пункт.
