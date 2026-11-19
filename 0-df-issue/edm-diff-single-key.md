# DF Issue: `Commit` кладёт в `ChangedValueObjects` только один ключ на событие

## Суть
`BusinessOperationData<TContext,TModel>.Commit(old, new)` (SeedWorks 0.6.0) должен класть в
`ChangedValueObjects` **все** изменившиеся value objects. Фактически на стенде в
`Commands."DomainEvents"` базы `client` у каждого события **ровно один ключ**, хотя команда
меняет несколько полей.

## Факты (замер @DO 2026-11-19 05:05, `Commands.DomainEvents` базы `client`)
| событие | команда | изменённые поля (по коду) | `ChangedValueObjectsJson` |
|---|---|---|---|
| v1 | `Register` | `Root`, `OtpCode`, `OtpExpiresAt` | `{"Root": …}` |
| v2 | `VerifyOtp` | `Verified`, `OtpCode`, `OtpExpiresAt`, `OtpAttempts` | `{"OtpCode": …}` |
| v3 | `UpdateProfile` | `DisplayName`, `Gender`, `PhotoUrl` | `{"Gender": …}` |

## Почему это не код BC
- `Client.Domain/Implementation/AnemicModel.cs` `GetValueObjects()` возвращает **все 8** полей
  (`Root`, `DisplayName`, `Gender`, `PhotoUrl`, `OtpCode`, `OtpExpiresAt`, `OtpAttempts`, `Verified`);
- `Aggregate.Success(m)` вызывает `BusinessOperationData<…>.Commit(Model, m)` и не трогает
  `ChangedValueObjects` — оно формируется внутри библиотеки;
- `BusAdapter.SaveEventAsync` просто сериализует `result.Event.ChangedValueObjects`
  (`JsonSerializer.Serialize(…, IValueObjectJsonConverter)`), ключи словаря не фильтрует.
- Тот же `Commit` используется в Journal/Partners (SeedWorks 0.6.0) — потеря происходит
  в общем пути, а не в конкретном BC.

## Воздействие
- Client: событие `VerifyOtp` несёт `{"OtpCode"}`, а не `Verified` → `verified=false`
  (закрыт workaround-ом #2892: явное обновление read-модели в `VerifyClientOtpHandler`).
- Journal: `AddRecord` меняет несколько полей, в read-модель попадает одно → запись не находится
  в `GET /journal/records` (smoke-front `wizard-record.spec.js:46`).
- Аналогично `UpdateProfile` → `displayName`/`photoUrl` не долетают до read-модели.

## Рекомендация
Исправить дифф в SeedWorks: `Commit` должен сравнивать `old.Invariants` vs `new.Invariants`
по **всем** ключам и возвращать все отличия (сейчас, судя по детерминированному паттерну,
берётся одно изменение). Точную строку не декомпилировал (ReflectionTypeLoadException при
загрузке DLL изолированно; пакет без .pdb/.xml).

## Workaround (временный, per-BC)
Явное обновление read-модели в обработчике команды, как в Client #2892
(`VerifyClientOtpHandler` → `Verified=true` до ответа). Это НЕ канон — «чинить» дифф в одном BC
было бы вторым источником правды (@DO 05:05), поэтому канон-фикс остаётся в библиотеке (BE-136).
