# Command API: Приватность и доступ

> Внутренний API (UI → сервис), без HTTP.

## Команды
| Команда | Функция (код) | Входные VO | Состояние до |
|---|---|---|---|
| SetPin | Pin.setPin(pin) | pin (4–8 цифр) | (нет) |
| VerifyPin | Pin.verifyPin(pin) | pin | Set |
| RemovePin | Pin.removePin() | — | Set |
| Lock | lock() | — | Unlocked |
| Unlock | unlock() | pin | Locked |

## Request DTO
```json
{ "pin": "string (4–8 цифр)" }
```

## Цепочка корреляции имён
```
UI → openPinDialog() → applyPin() → Pin.setPin(nw) → PinSet
UI → unlock() → Pin.verifyPin(pin) → showView("journal")
UI → lock() → Pin.hasPin() → showView("lock")
```
