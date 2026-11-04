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
- **PinCredential** — per-client: `clientId` (uuid, чей PIN); идентичность = `clientId` (ADR-021, AR-008).

**Объект-значения (Value Objects):**
- **Salt**: salt (base64)
- **Hash**: hash (hex, PBKDF2)
- **Iterations**: iterations (int)

**2.2. Состояние (`AnemicModel`) — атрибуты корня агрегата (AR-017).** Сверено с кодом 2026-11-15:
`IPinCredentialAnemicModel` (`Root`, `Locked`) + `PinCredentialRoot` (`ClientId`, `Salt`, `Hash`, `Iterations`).

| Атрибут | Тип в коде | Кард. | Обяз. | Задаётся | Инвариант | Источник |
|---|---|---|---|---|---|---|
| `Root.ClientId` | Guid | 1 | **да** | `SetPin` | скоуп: PIN клиента не читается другим клиентом (иначе чужой замок блокирует дневник) | ADR-024, ADR-008 |
| `Root.Salt` | string | 1 | **да** | `SetPin` | случайная соль на каждую установку PIN (`PinHasher_Salted_NotDeterministic`) | ADR-024 |
| `Root.Hash` | string | 1 | **да** | `SetPin` | **только хеш**, plaintext не хранится и не логируется; `Iterations = 100_000` | ADR-024 |
| `Root.Iterations` | int | 1 | да | `SetPin` | при повышении итераций старый PIN продолжает проверяться по своему значению (иначе смена политики ломает вход) | ADR-024 |
| `Locked` | bool | 1 | да (`false`) | `Lock`/`Unlock` | `Locked` — состояние UI-замка, не влияет на валидность PIN | ADR-024 |

**2.2.2. Расхождения:** `ClientId` живёт в **атрибутах Privacy**, хотя ADR-024 относит Privacy к контексту `Client` —
то есть скоуп владельца хранится дважды (в Client и здесь). Пока сервисы раздельные, это дублирование факта владения;
при слиянии (ADR-024) должно остаться одно место.

**AR-016: контракт этого контекста — только `operationId`, без пересказа путей и полей.**
Источник: `04-architecture/api-index.yaml` (гейт `check_api_index.py` сверяет индекс с маршрутами кода).

| Что | `operationId` |
|---|---|
| запись (команда) | `Privacy.ChangePin` |
| запись (команда) | `Privacy.Lock` |
| запись (команда) | `Privacy.Remove` |
| запись (команда) | `Privacy.SetPin` |
| запись (команда) | `Privacy.Unlock` |
| запись (команда) | `Privacy.VerifyPin` |
| запись (команда) | `Privacy.Token` |
| запись (команда) | `Privacy.VerifyToken` |
| чтение | `Privacy.HasPin` |

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
| SinglePinPerClient | один PIN на клиента (`clientId`) |
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
