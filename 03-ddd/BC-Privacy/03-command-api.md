# Command API: Приватность и доступ (Privacy)

> Именованные команды → методы агрегата → события. Id/Version — `<guid, long>` фреймворка EDM (DF).

## Команды
| Команда | Метод агрегата | Событие | Входные VO |
|---|---|---|---|
| SetPin | `SetPin(pin)` | `PinSet` | Salt, Hash, Iterations |
| ChangePin | `ChangePin(pin)` | `PinChanged` | Salt, Hash, Iterations |
| Remove | `Remove()` | `PinRemoved` | — |
| Lock | `Lock()` | `Locked` | — |
| Unlock | `Unlock()` | `Unlocked` | — |

## Request DTO
```json
{ "pin": "string (4–8 цифр)" }
```

## Цепочка корреляции имён
```
UI → SetPin(pin) → PinSet
UI → ChangePin(pin) → PinChanged
UI → Remove() → PinRemoved
UI → Lock() → Locked → Unlock() → Unlocked
```
