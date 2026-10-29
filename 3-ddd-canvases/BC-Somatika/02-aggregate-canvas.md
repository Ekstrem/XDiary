# Aggregate Design Canvas: SomaticPoint + DailyMetrics

> Bounded Context: Соматика
> Агрегаты: SomaticPoint (минутная точка), DailyMetrics (дневные сводки)
> Профиль: Append-only (импорт), Single-writer

## 1. Name (Имя)
- **SomaticPoint** — одно значение за минуту (t = «YYYY-MM-DDTHH:mm»).
- **DailyMetrics** — дневная сводка (стресс, пульс покоя, HRV) + сон.

## 2. Description (Описание)
Контекст хранит импортированные физиологические данные и отдаёт их Журналу (срезы) и Аналитике (тренды).

**Value Objects — SomaticPoint:**
- **Root**: t (string, минута)
- **HeartRate**: hr (int)
- **BodyBattery**: body_battery (int)

**Value Objects — DailyMetrics:**
- **Root**: date (string)
- **Stress**: stress (int) — в `weekly`
- **RestingHR**: resting_hr (int) — в `weekly`
- **Hrv**: hrv (int) — в `weekly`
- **Sleep**: score, durationMin, deepMin, lightMin, remMin, awakeMin — в `sleep`

## 3. State Transitions
```
(нет) → [ImportSomatic] → Stored
Stored → [ReimportSomatic] → Stored (upsert по t)
```

## 4. Enforced Invariants
| Инвариант | Описание |
|---|---|
| MinuteGranularity | t имеет формат «YYYY-MM-DDTHH:mm» |
| NonNegative | hr/body_battery/stress ≥ 0 |

## 5. Corrective Policies
| Политика | Триггер | Действие |
|---|---|---|
| JournalSliceRefresh | новые точки | Журнал пересчитывает срез вокруг акта |
| AnalyticsRefresh | новые дневные метрики | Аналитика пересчитывает тренды |

## 6. Handled Commands
| Команда | Функция (код) | Входные VO | Инициатор |
|---|---|---|---|
| SyncStress | garminSync → importWeekly(stress) | stress по дням | UI/автосинк |
| SyncSomatic | garminSync → DB.putSomaticBatch | точки (t,hr,bb) | UI/автосинк |
| SyncSleep | garminSync → DB.putSleepBatch | sleep по дням | UI/автосинк |
| SyncHrv | garminSync → importWeekly(hrv) | hrv по дням | UI/автосинк |
| ImportCsv | handleImportAnyFile | CSV | UI |

## 7. Created Events
| Событие | Изменённые VO | Когда |
|---|---|---|
| SomaticImported | SomaticPoint | SyncSomatic/ImportCsv |
| SleepImported | Sleep | SyncSleep/ImportCsv |
| StressImported | Stress | SyncStress/ImportCsv |
| HrvImported | Hrv | SyncHrv/ImportCsv |

## 8. Throughput
- Частота: пакетная (тысячи точек за синк), редкие вызовы.

## 9. Size
- Объём: месяцы данных, ~23k точек/месяц.
- Снапшоты: не нужны (append-only в IndexedDB).
