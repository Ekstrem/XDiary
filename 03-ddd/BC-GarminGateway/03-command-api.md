# Command API: Шлюз Garmin (GarminGateway)

> Команды → методы агрегата → события. Конвенция EDM/DF — **только POST**.
> **Per-client:** `clientId` берётся из JWT (`sub = clientId`) — каждый клиент подключает свой Garmin (ADR-021).
> **Единый UI-контракт Connect/Sync/State, коды и истечение:** `06-connect-contract.md` (AR-163, владелец 2026-11-20).

## Команды
| Команда | Эндпоинт (факт 6-msa) | Метод агрегата | Событие |
|---|---|---|---|
| Connect | `POST /api/v1/garmin/connect` | `Connect(credentials)` | `GarminConnected` |
| Sync | `POST /api/v1/garmin/sync` | `Sync(config)` | `GarminSynced` |
| FetchActivities | `POST /api/v1/garmin/fetch-activities` | `FetchActivities()` | `GarminActivitiesFetched` |
