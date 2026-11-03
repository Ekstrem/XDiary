# Нагрузочное тестирование (TE-010, TE-011)

Цель (из `04-architecture/vm-sizing.md`):
- **TE-010 (baseline):** 1 tpm (транзакция в минуту) на развёрнутой среде; ошибок/рестартов нет; CPU/RAM в пределах расчётных.
- **TE-011 (soak + пик):** длительный прогон (несколько часов) на 1 tpm — память стабильна; кратковременный burst (10 tpm) — система выдерживает и восстанавливается.

## Что нужно
- Запущенный стенд: шлюз + 10 сервисов + PostgreSQL (VPS или локальный `podman-compose` из `7-deploy`).
- Валидный JWT (после BE-014) или обход через `/api/v1/privacy/*` (не покрывает бизнес-нагрузку).
- Переменные окружения: `XDIARY_E2E_BASE_URL` (напр. `https://xdiary-vps1.taile0b6ee.ts.net`), `XDIARY_E2E_TOKEN`.
- Метрики стенда: `podman stats` / `docker stats` по контейнерам + `pg_stat_activity` / `pg_stat_database`.

## Скрипты
- `load.js` — k6 (Linux/CI). Сценарии `baseline_1tpm` (rate=1/мин) и `peak_burst` (rate=10/мин).
- `load.ps1` — PowerShell fallback (Windows, без внешних зависимостей): N итераций «1 запрос в минуту», пишет латентность/ошибки в CSV.

## Запуск k6
```bash
k6 run -e XDIARY_E2E_BASE_URL=... -e XDIARY_E2E_TOKEN=... load.js
```

## Запуск PowerShell
```powershell
$env:XDIARY_E2E_BASE_URL = "https://xdiary-vps1.taile0b6ee.ts.net"
$env:XDIARY_E2E_TOKEN    = "<access token>"
./load.ps1 -Minutes 10 -Out report.csv
```

## Отчёт
Заполнить по итогам: дата, стенд, сценарий, RPS/ошибки, p50/p95 латентности, CPU/RAM по сервисам и PostgreSQL, рестарты, вывод (в пределах расчёта / нет).

> Из среды тестировщика (Windows, без Tailscale/стека) прогон невозможен — нужен стенд/CI с доступом.
