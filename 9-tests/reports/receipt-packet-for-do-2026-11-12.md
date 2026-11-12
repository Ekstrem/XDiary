# Пакет квитанций для @DO: три прогона, которые закрывают `TE-054`, `TE-104`, `TE-050`

Дата: `2026-11-12`. Всё ниже — команды и критерии, а не пересказ. Инструмент квитанций —
`9-tests/scripts/check_run_receipt.py` (моя зона): он разбирает **и** Playwright-лог, **и** вывод
`dotnet test` (`Passed! - Failed: 0, Passed: 4, …`) и возвращает код `0/1/2` — «исполнялось и зелено» /
«есть падения» / «не измерено».

## 1. `front-e2e-repeat` — для `TE-054` и `TE-104` (один прогон, две спеки)

```bash
gh workflow run front-e2e-repeat.yml --ref main \
  -f specs='tests/wizard-offline.spec.js tests/startup-api-error.spec.js' \
  -f repeat=1 -f cleanup=yes -f workers=1
```

Ожидаемо: `repeat` = **success**, в шагах «Итог по спекам» — таблица по двум спекам, `Итог по спекам` —
success (падений нет). Затем:

```bash
gh run view <run_id> --log > run.log
python 9-tests/scripts/check_run_receipt.py --log run.log --run <run_id> --sha <short_sha>
# ожидаемо: rc=0, строка «квитанция прогона: REF=… | run=… | спек в логе 2 | тестов исполнено 2 | passed 2 | failed 0»
```

**Что вписать в карточки** (по строке из лога):

- `TE-054` → `## Результат`: «`XDIARY_READPATH_OFFLINE=1`, `npx playwright test tests/wizard-offline.spec.js` —
  зелёный, `REF=<sha>`, run `<id>`; падений 0». Критерий 1 → `[x]`.
- `TE-104` → то же для `tests/startup-api-error.spec.js`; критерий «браузерный прогон» → `[x]`.

Негативный контроль (`TE-054` п.3) — отдельный прогон: временно убрать `#view-lock`/фикс в `8-front`
(зона @FE) **или** прогнать на предыдущем коммите без фикса; спека обязана **упасть**. Без этого п.3
остаётся открытым — это не формальность, а доказательство, что спека не «зелёная на пустоте».

## 2. `ci-backend` — для `TE-050` (тесты мягкого удаления)

```bash
gh workflow run ci-backend.yml --ref main     # dispatch ⇒ all=1 ⇒ все BC
```

Нужен **не** просто success: в логе шага «Тесты изменённых BC» должны быть строки про
`Journal.ApiTests` / `PartnerSoftDeleteTests`. Быстрый локальный дубль (если стенд доступен):

```bash
XDIARY_E2E_BASE_URL=https://xdiary-vps2.taile0b6ee.ts.net \
  dotnet test 6-msa/BC-Journal/src/Journal.ApiTests/Journal.ApiTests.csproj \
  --filter "FullyQualifiedName~PartnerSoftDeleteTests"
```

Ожидаемо `Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4`. Квитанция из вывода:

```bash
dotnet test … > test.log 2>&1
python 9-tests/scripts/check_run_receipt.py --log test.log --sha <short_sha>
# ожидаемо: rc=0, «источник dotnet-test | passed 4 | failed 0»; при Total: 0 — rc=2 «НЕ ИЗМЕРЕНО»
```

Четыре теста: `Remove_PartnerDisappearsFromList`, `Remove_IsIdempotent_SecondCallIsNotServerError`,
`Remove_ThenGet_ReturnsNotFoundOrForbidden_NotOk`, `Remove_PartnerInExistingJournalRecord_StillResolvable`.

## 3. Почему это не «ещё один прогон»

| Карточка | Что уже доказано | Чего не хватает |
|---|---|---|
| `TE-054` | критерии 2/4/5 закрыты (`[x]`), флаг в двух workflow, порядок коммитов замерен | прогон + негативный контроль |
| `TE-104` | все статические критерии `[x]`, спека прошла dryrun (29 тестов/0 падений/309 утверждений) | браузерный прогон |
| `TE-050` | тест-проект собирается (`Ошибок: 0`), 4 теста на месте, блокер `BE-098` снят | прогон с явными строками `PartnerSoftDeleteTests` |

`check_run_receipt.py --selftest` — **6/6** (Playwright: норма/падение/skipped; `dotnet test`: 0/1/2),
поэтому «зелёный при неисполненных тестах» он не пропустит.
