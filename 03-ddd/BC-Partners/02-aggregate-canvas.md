# Aggregate Design Canvas: Partner (Партнёр)

> Bounded Context: Партнёры (Partners)
> Агрегат: Partner
> Профиль: Single-writer

## 1. Name (Имя)
- **Имя:** Partner (Партнёр).
- **Создаётся когда:** пользователь регистрирует партнёра.
- **Завершается когда:** удаление партнёра.

## 2. Description (Описание)
Агрегат Partner хранит профиль партнёра. Поставляет выбор партнёра и его пол в Журнал.

**2.1. Идентификатор и корень (DF):**
- Составной ключ `<guid, long>`: `guid` идентифицирует агрегат, `long` — версия. Даёт фреймворк, **доменным полем не является**.
- Корень: `IPartnerRoot : IValueObject` — содержит **`Name`** (единственный «идентифицирующий» признак партнёра).

**2.2. Состояние (`AnemicModel`) — атрибуты корня агрегата** (сверено с кодом `6-msa/BC-Partners`, 2026-11-10):

| Атрибут | Тип | Кард. | Обяз. | Задаётся | Инвариант |
|---|---|---|---|---|---|
| `root.name` | string | 1 | да | `Register(name, clientId)` / `Rename(name)` | `NameRequiredValidator` (`NameRequired`) |
| `gender` | string (`male` \| `female`) | 1 | да (валидируется при `UpdateProfile`) | `UpdateProfile(photo, gender, …)` | `GenderEnumValidator` — **строго словарь** `male`/`female` |
| `photo` | string (dataURL, 512px) | 0..1 (пустая строка = нет) | нет | `UpdateProfile` | — |
| `notes` | string | 0..1 | нет | `UpdateProfile` | — |
| `clientId` | Guid? | 0..1 | нет | `Register` (владелец), `LinkClient` / `UnlinkClient` | связь с аккаунтом клиента, **опционален** (партнёр может стать клиентом, ADR-024) |

**Технические поля** (не доменные, даёт фреймворк): `Id`, `Version`, `CorrelationToken`, `CommandName`, `SubjectName`.

**2.3. Что НЕ хранится (и почему):**
- **Профиль «я»** (`displayName`/`gender` клиента) — в `Client`; в `Partner` не проецируется (ADR-024: одно имя не живёт в двух местах);
- **История актов с партнёром** — в `Record.partners[]` (ссылка по `id`), не в партнёре;
- **Мягкое удаление / `state`:** **нет** — `Remove()` в агрегате не меняет состояние (`Success(Clone())`), удаление физическое на уровне хранения. Поэтому поля `deletedAt`/`state` в модели отсутствуют — и это надо помнить при E1: событие `PartnerRemoved` фиксирует факт, а «удалённость» в модели не выражается.

**2.4. Известное расхождение (найдено при сверке 2026-11-10):** `GenderEnumValidator` в этом BC сравнивает **без нормализации** (`gender is "male" or "female"`), а канон для профиля клиента (`ADR-024 §3`) требует `Trim().ToLowerInvariant()` → словарь. Один справочник пола, два поведения: `Male` в `Partner` → отказ, в `Client` (после BE-063) → `male`. **Правило должно быть одно:** нормализация на входе, канон в модели — иначе у партнёра и у «я» разные допустимые написания.

## 3. State Transitions
```
(нет) → [Register] → Active
Active → [Rename / UpdateProfile] → Active
Active → [Remove] → Removed
```

## 4. Enforced Invariants
| Инвариант | Описание |
|---|---|
| NameRequired | name обязателен |
| GenderEnum | gender ∈ {male, female} |

## 5. Corrective Policies
| Политика | Триггер | Действие |
|---|---|---|
| JournalSync | PartnerRenamed / PartnerRemoved | Журнал читает актуальное имя/пол |

## 6. Handled Commands (именованные методы)
| Метод | Входные VO |
|---|---|
| `Register(name)` | name |
| `Rename(name)` | name |
| `UpdateProfile(photo, gender, notes)` | Photo, Gender, Notes |
| `Remove()` | — |

## 7. Created Events (каждый метод → событие)
| Метод | Событие |
|---|---|
| `Register` | `PartnerRegistered` |
| `Rename` | `PartnerRenamed` |
| `UpdateProfile` | `PartnerProfileUpdated` |
| `Remove` | `PartnerRemoved` |

## 8. Throughput
- Частота: единицы (редкое редактирование профилей).

## 9. Size
- Событий за жизнь: 1..несколько. Снапшоты не нужны.
