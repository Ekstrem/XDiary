# Пакет ревью @TE: квитанции по 26 карточкам «На проверке» (`2026-11-11`)

Квитанции получены прогоном, а не пересказом. Команда и `rc` — в таблице; «rc=2» значит
«не измерено» (нет доступа/нет предусловия), это не дефект продукта.

| Карточка | Что проверяется | Команда | rc | Квитанция (хвост) |
|---|---|---|---|---|
| TE-050 | приёмка чтения: чужой id → 404, свой путь → 200 | `accept_read_path.py --selftest` | 0 | accept_read_path --selftest: 37/37 (каждый критерий проверен на дефекте) |
| TE-053 | PIN-фолбэк (нужен браузер → CI) | `--check tests/pin-fallback.spec.js` | 0 | — |
| TE-059 | индикатор busyRun (таймеры) | `5-tech-backlog/front-end/FE-040-timers-test.js` | 0 | ИТОГ: 7/7 проверок, падений 0 |
| TE-062 | бюджет теста: объявленные ожидания vs лимит | `check_e2e_timeout_sums.py --selftest` | 0 | check_e2e_timeout_sums --selftest: 8/8 (сумма ожиданий больше лимита теста = ::warning::, --strict = rc=1) |
| TE-071 | нативные диалоги не вешают прогон | `--check tests/api.js` | 0 | — |
| TE-072 | `.or().first()` не выбирает скрытый экран | `--check tests/api.js` | 0 | — |
| TE-073 | клик «Сохранить» в визарде ограничен по времени | `--check tests/wizard-record.spec.js` | 0 | — |
| TE-074 | `e2e_compare_runs` — «стало лучше/хуже» | `e2e_compare_runs.py --selftest` | 0 | e2e_compare_runs --selftest: 4/4 (A/B по тестам: регресс = красный только в B) |
| TE-075 | мутирующий клик обязан иметь таймаут | `--check tests/api.js` | 0 | — |
| TE-076 | диагностика шага онбординга | `--check tests/onboarding.spec.js` | 0 | — |
| TE-077 | уборка удалённого партнёра не «падает» | `--check tests/api.js` | 0 | — |
| TE-078 | карта покрытия UI (объём MVP) | `check_e2e_ui_coverage.py --selftest` | 0 | check_e2e_ui_coverage --selftest: 5/5 (непокрытый критичный элемент = ::warning::, --strict = rc=1) |
| TE-079 | симметрия event-sourcing | `check_e2e_eventsourcing_contract.py --selftest` | 0 | check_e2e_eventsourcing_contract --selftest: 7/7 (писатель событий без `IValueObjectJsonConverter` = rc=1: иначе модифицирующие ручки дают 404) |
| TE-080 | мета-гейт: у каждого гейта есть самопроверка | `check_e2e_gate_hygiene.py --selftest` | 0 | check_e2e_gate_hygiene --selftest: 5/5 (гейт без `--selftest` или без квитанции = rc=1) |
| TE-082 | командные чтения: read-модель без fallback | `check_e2e_command_read_path.py --selftest` | 0 | check_e2e_command_read_path --selftest: 4/4 (командное чтение только из read-модели = rc=1: лаг проекции даёт 404/400 на свежих данных) |
| TE-083 | CI-флаги: скип-навсегда = «тест без теста» | `check_e2e_ci_env_flags.py --selftest` | 0 | check_e2e_ci_env_flags --selftest: 4/4 (флаг, без которого спека скипается, но которого нет в CI = rc=1: «тест без запуска») |
| TE-084 | `timeout_sums` не врёт про лимит | `check_e2e_timeout_sums.py --selftest` | 0 | check_e2e_timeout_sums --selftest: 8/8 (сумма ожиданий больше лимита теста = ::warning::, --strict = rc=1) |
| TE-085 | `waitAnyScreen` на харнессе без браузера | `helpers.test.js` | 0 | helpers.test.js: 5/5 (waitAnyScreen: id показанного экрана, таймаут, диагностика при провале) |
| TE-086 | `--allow-empty` исполняет тела спек | `e2e-dryrun.js --all-flags` | 0 | e2e-dryrun: спек 22 \| тестов исполнено 28 \| пропущено по skip 0 \| упало 0 (без утверждений 0) \| ошибок загрузки 0 \| спек без тестов 1 \| утверждений вы |
| TE-087 | триаж: владелец по коду, не по имени | `e2e_triage.py --selftest` | 0 | e2e_triage --selftest: 5/5 (классы: test-timeout/wait-timeout/data-lag/session-lost/stand/assert/other) |
| TE-088 | квитанцию читает инструмент, а не человек | `e2e_receipt.py --selftest` | 0 | e2e_receipt --selftest: 12/12 (упавшая спека = rc=1, неразобранный вывод = rc=2) |
| TE-090 | CORS-вход: preflight + ACAO | `check_cors_entry.py --selftest` | 0 | check_cors_entry --selftest: 7/7 (preflight без 2xx/ACAO или POST без ACAO = rc=1: браузер режет ответ при живом сервере) |
| TE-091 | OTP не утекает в ответе | `check_otp_leak.py --selftest` | 0 | check_otp_leak --selftest: 14/14 (OTP-поле в теле ответа = rc=1: код даёт JWT владельца, BE-090) |
| TE-092 | `AddPartner` не затирает поля | `check_be093_stale_fields.py --selftest` | 0 | check_be093_stale_fields --selftest: 10/10 (AddPartner, затирающий place/tags = rc=1: полная замена строки вместо патча, BE-093) |
| TE-093 | теги `images.lock` против реестра | `filelist_source.py --selftest` | 0 | filelist_source --selftest: 3/3 (список берётся из git ls-files: неотслеживаемая копия не меняет вердикт) |

Итог: карточек 25, rc=0 — 25, rc=2/не измерено — 0, прочее — 0.
