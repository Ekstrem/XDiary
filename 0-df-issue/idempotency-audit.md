# Аудит идемпотентности POST-операций (цель владельца «все POST идемпотентны»)

Механизмы по памятке `idempotency-playbook.md`. Статус на `role/be`.

## Create-POST (дубль при двойном тапе/retry)

| BC | POST | Механизм | Статус |
|---|---|---|---|
| Catalog | `/api/v1/catalog` | клиентский `id` + upsert | ✅ сделано (`f3a0e76bd`) |
| Partners | `/api/v1/partners` | клиентский `id` + upsert | ✅ сделано (`d5e6830be`) |
| Journal | `/api/v1/journal/records` | клиентский `id` (`?id=`) + upsert | ✅ сделано (`7e50f4b70`) |
| Portability | `/api/v1/portability/import/*` | клиентский `id` + upsert | ✅ сделано (`a72b087be`) |
| Client | `/api/v1/clients` | register-or-login по телефону | ✅ уже (BE-092) |
| AppleHealthGateway | `configure`/`import` | natural key `(clientId, source)` | ✅ уже |
| Analytics | `/api/v1/analytics/events` | `id` события → snapshot upsert | ✅ уже |
| Journal | `/api/v1/journal/records/from-garmin` | dedup по `garminId` | ✅ сделано (`ca3eb9e58`) |

## Action-POST (мутация существующего агрегата по `id`)

Агрегаты применяют **абсолютное присваивание** (`Clone()` + `set`) или **dedup** — повтор той же
команды даёт то же состояние, без дубля (идемпотентность по определению, п.1 памятки).

Проверено по коду:
- **Journal**: `SpecifyWhen`, `AddPartner` (dedup `Contains`), `RemovePartner`, `SpecifyPlace`,
  `AddPositions`/`AddAccessories`/`RecordContraception` (абсолютное `= ...ToList()`), `SpecifyDuration`,
  `RecordOrgasms`, `Complete`, `Delete` — все абсолютные/dedup;
- **Catalog**: `Rename`, `UpdateDetails`, `Archive`, `Restore`, `Remove` — абсолютные;
- **Partners**: `Register`, `Rename`, `UpdateProfile`, `Remove`, `Link/Unlink` — абсолютные.

## Остаток

Нет. Все create-POST имеют механизм идемпотентности (клиентский `id` / natural key), все
action-POST идемпотентны по построению (абсолютное присваивание/dedup). `from-garmin` —
dedup по `garminId` на уровне хендлера (повторный синк → та же запись); частичный уникальный
индекс `WHERE garminId <> ''` можно добавить как DB-страховку отдельной миграцией.
