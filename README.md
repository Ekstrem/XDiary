# XDiary

Приватный дневник пары: фиксация сексуальной активности. Локально, без облака.

## Что уже есть (MVP)

- **Запись акта (визард-диалог):** шаг за шагом — дата/время, партнёр, место, позы (каталог + свои), особые отметки (тэги), длительность, оргазмы партнёров.
- **Партнёры:** отдельная вкладка — карточки с фото и подробностями; при записи последний партнёр выбран по умолчанию.
- **Каталог:** костюмчики / игрушки / фантазии (фото + подробности); на экране «Отметки» они выбираются из каталога.
- **Соматика (Garmin):** импорт CSV (пульс, стресс, Body Battery, HRV) + график на записи; импорт сна (оценка, стадии).
- **Журнал:** список, просмотр записи (с соматикой), редактирование, удаление.
- **Хранение:** IndexedDB (на устройстве) + экспорт/импорт в CSV (открывается в Excel/Sheets).
- **Приватность:** PIN-код на входе (PBKDF2-SHA256, хранится только хеш), автоблокировка после долгого скрытия.
- **PWA:** устанавливается на Android, работает офлайн (service worker).

Технологии: статический PWA (HTML/CSS/JS, без сборки и бэкенда).

## Структура

```
index.html            — каркас приложения
manifest.webmanifest  — манифест PWA
sw.js                 — service worker (офлайн)
css/styles.css        — стили (тёмная тема, mobile-first)
js/db.js              — IndexedDB
js/pin.js             — PIN (PBKDF2)
js/csv.js             — CSV (RFC 4180)
js/app.js             — логика и UI
icons/                — иконки
samples/              — тестовые CSV (соматика, сон)
tools/gen_icons.py    — генерация иконок (Pillow)
tools/gen_sample.py   — генерация тестовых CSV
tools/garmin_sync.py  — синхронизация через Garmin Personal API -> CSV
tools/smoke_csv.js    — smoke-тест CSV
tools/smoke_garmin.js — тест парсинга Garmin-отчётов
01-user-stories/      — бэклог и этапы
02-cjm/               — CJM инкрементов
```

## Запуск локально

Приложение — статические файлы, нужен любой HTTP-сервер (PWA требует HTTPS/localhost):

```bash
# из корня репозитория
python -m http.server 8000
# открыть http://localhost:8000
```

## Деплой на GitHub Pages

1. Заложить файлы в ветку `main` (уже готово).
2. В репозитории: Settings → Pages → Source: `Deploy from a branch`, ветка `main`, папка `/ (root)`.
3. Открыть `https://<user>.github.io/XDiary/`.

## Установка на Android

1. Открыть URL в Chrome.
2. Меню → «Добавить на главный экран» (или «Установить приложение»).
3. Запускать как обычное приложение, работает офлайн.

## Соматика (Garmin)

Импорт — в настройках → «Соматика (Garmin)» → «Импорт данных Garmin…». Диалог сам определяет тип файла:

- **Отчёты Garmin Connect** (Reports → Export): недельный стресс, пульс покоя, сон.
- **Соматика по минутам** (наш формат): `timestamp,hr,stress,body_battery,hrv`.
- **Сон по дням** (наш формат): `date,sleep_score,duration_min,deep_min,light_min,rem_min,awake_min`.

Недельные данные видны на вкладке **«Тренды»** (стресс / пульс покоя / сон). Поминутная соматика отображается на записи в окне «±N минут вокруг акта».

Для теста без Garmin: `python tools/gen_sample.py` и импортируйте `samples/sample_somatic.csv` / `samples/sample_sleep.csv`.

## Garmin Connect Personal API (синхронизация)

Альтернатива ручному экспорту — официальный «Личный API-ключ» Garmin Connect.

1. Garmin Connect Web → **Personal API Key** → создать ключ (30/90/180/365 дней). Ключ показывается один раз.
2. Скопировать ключ в `.env` (файл в `.gitignore`):
   ```
   GARMIN_API_KEY=GCDC_...
   ```
3. Запустить (Python 3, без зависимостей):
   ```bash
   python tools/garmin_sync.py --from 2026-10-21 --to 2026-10-28
   ```
4. Скрипт сам получает OAuth2-токен
   (`POST /personalapitoken-service/personal-api/personal-api-key/token`, заголовок `Authorization: ApiKey <ключ>`)
   и кладёт в `garmin_export/`:
   - `somatic.csv` — пульс (поминутно) + Body Battery → импорт «Соматика (минуты)»;
   - `sleep.csv` — сон по дням → импорт «Сон (дни)»;
   - `stress.csv` — стресс по дням → импорт как Garmin-отчёт;
   - `resting_hr.csv` — пульс покоя по дням → импорт как Garmin-отчёт.
5. В приложении: Настройки → «Соматика (Garmin)» → «Импорт данных Garmin…» (по одному файлу).

Используемые endpoint'ы: `dailyHeartRate`, `bodyBattery/reports/daily` (`chunks=288`), `dailySleep`,
`stats/stress/daily`, `usersummary/daily`. Токен живёт 24 ч; когда ключ истечёт — сгенерировать новый.

**Безопасность:** `.env` не коммитится. Ключ, попавший в переписку, стоит пересоздать.

## Приватность

- Все данные хранятся только на устройстве (IndexedDB), никуда не отправляются.
- PIN хранится в виде хеша (PBKDF2-SHA256, 100 000 итераций, случайная соль).
- Экспорт CSV — только по вашему действию; импорт объединяет записи по `id`.

## Дорожная карта

См. [01-user-stories/backlog.md](01-user-stories/backlog.md) (этапы) и [02-cjm/](02-cjm/) (карты путешествия). Дальше: скрипт выгрузки из Garmin Connect → каталог игр → статистика/корреляции → автоперенос из Garmin.
