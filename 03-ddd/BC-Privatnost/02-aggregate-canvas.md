# Aggregate Design Canvas: PinCredential (PIN-доступ)

> Bounded Context: Приватность и доступ
> Агрегат: PinCredential
> Профиль: Single-writer

## 1. Name (Имя)
- **Имя:** PinCredential (учётные данные PIN)
- **Создаётся когда:** пользователь устанавливает PIN (Pin.setPin)
- **Завершается когда:** удаление PIN (Pin.removePin) или полная очистка (wipeAll)

## 2. Description (Описание)
Агрегат хранит только хеш PIN (PBKDF2-SHA256 с солью). Открытый PIN не хранится.

**Value Objects:**
- **Root**: pin (в `meta` по ключу «pin»)
- **Salt**: salt (base64)
- **Hash**: hash (hex, PBKDF2)
- **Iterations**: iterations (100000)

## 3. State Transitions
```
(нет) → [SetPin] → Set
Set → [ChangePin] → Set
Set → [RemovePin] → (нет)
```

## 4. Enforced Invariants
| Инвариант | Описание |
|---|---|
| PinFormat | 4–8 цифр (Pin.isValidPin) |
| NoPlaintext | открытый PIN не сохраняется |

## 5. Corrective Policies
| Политика | Триггер | Действие |
|---|---|---|
| AutoLock | скрытие приложения > 2 мин | lock() → экран блокировки |

## 6. Handled Commands
| Команда | Функция (код) | Входные VO | Инициатор |
|---|---|---|---|
| SetPin | Pin.setPin(pin) | pin | UI |
| VerifyPin | Pin.verifyPin(pin) | pin | UI (unlock) |
| RemovePin | Pin.removePin() | — | UI |
| Lock | lock() | — | система/UI |
| Unlock | unlock() | pin | UI |

## 7. Created Events
| Событие | Изменённые VO | Когда |
|---|---|---|
| PinSet | Hash/Salt | SetPin |
| PinRemoved | — | RemovePin |
| Locked / Unlocked | состояние | Lock/Unlock |

## 8. Throughput
- Частота: редкие (установка/смена PIN), частая проверка при входе.

## 9. Size
- 1 запись в `meta`. Снапшоты не нужны.
