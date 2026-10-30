# Command API: Шлюз Garmin (GarminGateway)

> Именованные команды → методы агрегата → события. Id/Version — `<guid, long>` фреймворка EDM (DF).

## Команды
| Команда | Метод агрегата | Событие | Входные VO |
|---|---|---|---|
| Connect | `Connect(credentials)` | `GarminConnected` | Credentials |
| Sync | `Sync(config)` | `GarminSynced` | SyncConfig |
| FetchActivities | `FetchActivities()` | `GarminActivitiesFetched` | — |

## Request DTO
```json
{ "credentials": { "apiKey": "…", "oauthToken": "…" }, "config": { "metrics": ["hr","bb","stress","sleep","hrv"], "windowDays": 30 } }
```

## Цепочка корреляции имён
```
UI → Connect(credentials) → GarminConnected
UI/автосинк → Sync(config) → GarminSynced (норм. соматика → SomaticSeries/DailyMetrics)
UI «Из Garmin» → FetchActivities() → GarminActivitiesFetched (занятия → Journal)
```
