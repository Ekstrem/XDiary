# Aggregate Design Canvas: PinCredential (PIN-доступ)

> Bounded Context: Приватность и доступ (Privacy)
> Агрегат: PinCredential
> Профиль: Single-writer

## 1. Name (Имя)
- **Имя:** PinCredential (учётные данные PIN).
- **Создаётся когда:** пользователь устанавливает PIN.
- **Завершается когда:** удаление PIN или полная очистка.

## 2. Description (Описание)
Агрегат хранит только хеш PIN (PBKDF2-SHA256 с солью). Открытый PIN не хранится.

**Идентификатор агрегата (DF):**
- Составной ключ `<guid, long>` — технический, для **быстрого поиска**. НЕ входит в сущность.

**Сущность (Entity — корень агрегата):**
- **PinCredential** — синглтон (один на устройство); идентичность неявная (владелец).

**Объект-значения (Value Objects):**
- **Salt**: salt (base64)
- **Hash**: hash (hex, PBKDF2)
- **Iterations**: iterations (int)

## 3. State Transitions
```
(нет) → [SetPin] → Set
Set → [ChangePin] → Set
Set → [Remove] → (нет)
Set → [Lock] → Locked
Locked → [Unlock] → Set
```

## 4. Enforced Invariants
| Инвариант | Описание |
|---|---|
| PinFormat | 4–8 цифр |
| NoPlaintext | открытый PIN не сохраняется |

## 5. Corrective Policies
| Политика | Триггер | Действие |
|---|---|---|
| AutoLock | скрытие приложения > 2 мин | Lock() → экран блокировки |

## 6. Handled Commands (именованные методы)
| Метод | Входные VO |
|---|---|
| `SetPin(pin)` | Salt, Hash, Iterations |
| `ChangePin(pin)` | Salt, Hash, Iterations |
| `Remove()` | — |
| `Lock()` | — |
| `Unlock()` | — |

## 7. Created Events (каждый метод → событие)
| Метод | Событие |
|---|---|
| `SetPin` | `PinSet` |
| `ChangePin` | `PinChanged` |
| `Remove` | `PinRemoved` |
| `Lock` | `Locked` |
| `Unlock` | `Unlocked` |

## 8. Throughput
- Частота: редкие (установка/смена), частая проверка при входе.

## 9. Size
- 1 запись. Снапшоты не нужны.
