# InMemory PublishAsync → event-store replay race (BE-145 follow-up)

Дата: 2026-11-23 (@TE 17:13)

## Симптом на dev

`probe_journal_recent` → `rc=1`: `startedAt=0001-01-01`, `place=null`, `lastPlace=null`,
при этом `lastPartner` заполнен. То есть command-часть работает, partners доходит,
а `when`/`place` — нет.

## Почему это не проекция

`JournalProjection.ApplyVos` корректно обрабатывает `Root.StartedAt` и `Place`.
Проблема раньше: `AggregateProvider.GetAggregateAsync` делает replay из event store.

После BE-143/BE-145:
1. `BusAdapter.OnNext` вызывает `_bus.PublishAsync(...)`.
2. `InMemoryEventBus.PublishAsync` вызывает `observer.OnNext` и **сразу возвращает `Task.CompletedTask`**.
3. `*EventStoreHandler.OnNext` запускает `HandleAsync` fire-and-forget; сохранение события
   идёт асинхронно.
4. `await completion` завершается на этом «publish», но НЕ на записи в event store.
5. Следующая HTTP-команда может прийти до `SaveEventAsync` и сделать replay старого состояния.

Исходный код библиотеки:
`EventBus.InMemory/InMemoryEventBus.cs`: `PublishAsync` → `Publish(domainEvent); return Task.CompletedTask;`.

## Почему это именно гонка, а не бизнес-инвариант

- До BE-143 `BusAdapter` сохранял событие синхронно до publish, поэтому replay следующей команды
  видел предыдущее состояние.
- Теперь completion намеренно не включает event-store write (по guide).
- Поэтому «command → следующий replay» не имеет read-your-writes гарантии.

## Нужно решение @DR/@AR (не делаю самовольно)

Варианты:
- **A.** Для InMemory-профиля сделать durable event-store subscriber, чья запись завершается
  до возврата `PublishAsync` (иначе completion не является durable).
- **B.** Изменить completion/провайдер так, чтобы command-side replay ждал сохранения события,
  оставив MV асинхронной.
- **C.** Изменить InMemoryEventBus так, чтобы `PublishAsync` await-ил `IDomainEventHandler.HandleAsync`
  (требуется изменение библиотеки, а не только XDiary).

Пока не выбран вариант, фикс проекции не поможет: причина в гонке записи event store.
