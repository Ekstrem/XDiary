# Расход ресурсов (prod/dev)

> Авто-снимок `tools/resource-usage.py` — **не редактировать вручную**.
> Обновлён: 2026-11-01 20:42 UTC

## PROD (VPS-1)

- **Хост:** `31.192.111.168`
- **CPU:** 4 vCPU
- **RAM:** 5.8Gi total / 681Mi used / 4.4Gi avail
- **Диск `/`:** 9.9G used / 118G total
- **Load:** 0.02, 0.07, 0.13
- **Суммарно контейнеры:** ~810 MB

| Контейнер | CPU% | MEM | Limit% |
|---|---|---|---|
| `xdiary-pg` 🗄️ | 2.85% | 256.1MB | 47.70% |
| `xdiary-journal` | 2.34% | 56.25MB | 20.95% |
| `xdiary-portability` | 2.25% | 54.86MB | 20.44% |
| `xdiary-daily-metrics` | 2.26% | 54.85MB | 20.43% |
| `xdiary-somatic-series` | 2.25% | 54.43MB | 20.28% |
| `xdiary-partners` | 2.33% | 54.37MB | 20.25% |
| `xdiary-privacy` | 2.23% | 54.29MB | 20.23% |
| `xdiary-catalog` | 2.21% | 54.28MB | 20.22% |
| `xdiary-garmin-gateway` | 2.23% | 54.08MB | 20.15% |
| `xdiary-apple-health-gateway` | 2.21% | 53.99MB | 20.11% |
| `xdiary-analytics` | 1.24% | 32.51MB | 12.11% |
| `xdiary-api-gateway` | 8.61% | 30.16MB | 11.24% |

## DEV (VPS-2)

- **Хост:** `77.91.73.67`
- **CPU:** 2 vCPU
- **RAM:** 1.9Gi total / 943Mi used / 257Mi avail
- **Диск `/`:** 6.2G used / 58G total
- **Load:** 0.06, 0.77, 1.02
- **Суммарно контейнеры:** ~1385 MB

| Контейнер | CPU% | MEM | Limit% |
|---|---|---|---|
| `xdiary-journal` | 1.44% | 182.5MB | 67.99% |
| `xdiary-partners` | 1.12% | 161MB | 59.98% |
| `xdiary-catalog` | 1.20% | 155.8MB | 58.06% |
| `xdiary-privacy` | 3.21% | 152.7MB | 56.88% |
| `xdiary-pg` 🗄️ | 1.66% | 140.3MB | 26.14% |
| `xdiary-client` | 6.33% | 108.8MB | 40.52% |
| `xdiary-daily-metrics` | 5.97% | 101.4MB | 37.77% |
| `xdiary-api-gateway` | 1.16% | 89.75MB | 33.43% |
| `xdiary-somatic-series` | 5.08% | 88.48MB | 32.96% |
| `xdiary-apple-health-gateway` | 2.51% | 55.96MB | 20.85% |
| `xdiary-portability` | 2.50% | 55.94MB | 20.84% |
| `xdiary-garmin-gateway` | 2.55% | 55.89MB | 20.82% |
| `xdiary-analytics` | 1.36% | 36.03MB | 13.42% |
