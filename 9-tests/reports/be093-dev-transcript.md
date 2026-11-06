# Транскрипт приёмки `BE-093` на dev (пошагово, с телами запросов)

Стенд: `https://xdiary-vps2.taile0b6ee.ts.net` (`journal single-fdf7550`).
Дата: 2026-11-06. Все запросы — с `Origin: https://ekstrem.github.io`.

| # | Шаг |
|---|---|
| 1 | `POST /api/v1/clients` → **200** ```json {"phone": "+79990000590"} ``` |
| 2 | `POST /api/v1/clients/23399cef-b3db-4ece-b985-4913ec363cf7/verify` → **200** ```json {"code": "000000"} ``` |
| 3 | токен получен: длина 237 симв. |
| 4 | `POST /api/v1/partners` → **200** ```json {"name": "TE-transcript"} ``` |
| 5 | `POST /api/v1/journal/records` → **200** ```json {"when": "2026-11-08T10:00:00Z"} ``` |
| 6 | запись: `fbc95c0e-d275-4603-bbb8-a8cfd7e28419` |
| 7 | `/api/v1/journal/records/fbc95c0e-d275-4603-bbb8-a8cfd7e28419/when` → **400** |
| 8 | `POST /api/v1/journal/records/fbc95c0e-d275-4603-bbb8-a8cfd7e28419/place` → **200** ```json {"place": "Тестовое место TE"} ``` |
| 9 | `POST /api/v1/journal/records/fbc95c0e-d275-4603-bbb8-a8cfd7e28419/accessories` → **200** ```json {"tags": ["tag-a", "tag-b"], "costumes": [], "toys": [], "fantasies": []} ``` |
| 10 | `GET /api/v1/journal/records/fbc95c0e-d275-4603-bbb8-a8cfd7e28419` → **200** |
| 11 | чтение ДО: `place='Тестовое место TE'`, `tags=['tag-a', 'tag-b']` |
| 12 | `POST /api/v1/journal/records/fbc95c0e-d275-4603-bbb8-a8cfd7e28419/partners` → **200** ```json {"partnerId": "a3cfefa0-ac87-4f09-8e29-aa79d2cc7357"} ``` |
| 13 | `GET /api/v1/journal/records/fbc95c0e-d275-4603-bbb8-a8cfd7e28419` → **200** |
| 14 | чтение ПОСЛЕ: `place='Тестовое место TE'`, `tags=['tag-a', 'tag-b']` |

## Зачем транскрипт

@TL (`23:16`) просил свести квитанцию к «1 поле (place)», опираясь на сообщение @DO, что у `tags` нет сеттера. Транскрипт показывает обратное: `POST /records/{id}/accessories` с телом `{"tags":["tag-a","tag-b"],"costumes":[],"toys":[],"fantasies":[]}` возвращает `200`, и следующее чтение отдаёт `tags` с этими значениями. Поэтому в квитанции теперь указан **источник каждого поля**: `place=/place, tags=/accessories` — чтобы «2 поля» не выглядело как нечто без основания.
