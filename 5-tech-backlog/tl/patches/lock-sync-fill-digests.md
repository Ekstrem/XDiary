# Патч: шаг --fill-digests в lock-sync (DO-113)

**Зона:** @DO (.github/workflows/lock-sync.yml). **Автор патча:** @TL, 2026-11-18 13:05.
**Применение проверено:** git apply --check → rc=0 к main = a8f8e9322.

## Причина

lock-sync перегенерирует images.lock через --from-registry --write. При смене релизного тега **все** сервисы получают новый single-<sha>, а merge_digests сохраняет digest **только для того же тега** ⇒ 12 digest-строк → 0 ⇒ гейт DO-113 откатывает lock ⇒ новые образы не выкатываются (ровно это и было в прогоне 44277407384).

## Правка

Перед шагом Guard добавляется:

`yaml
      - name: Заполнить digest из реестра (DO-113)
        run: python tools/gen_images_lock.py --fill-digests || true
`

|| true — потому что ront не публикуется (NO_REGISTRY_SERVICES), для него digest не появится.

## Сквозная симуляция (2026-11-18 15:06, worktree origin/main)

| шаг | действие | digest=sha256: | Guard --check-digests |
|---|---|---|---|
1 | исходный lock | 12 (+ront = -) | — |
2 | имитация перегенерации с новым тегом | 0 | — |
3 | Guard **без** патча | 0 | **rc=1** «было 12, стало 0» — дословно ошибка 44277407384 |
4 | --fill-digests | 12 (без образа: ['front']) | — |
5 | Guard **с** патчем | 12 | **rc=0** «12 (в HEAD 12) — не уменьшилось» |

## Как проверить после применения

1. lock-sync на новом main — зелёный, в images.lock **новый тег и ≥12 digest=sha256:**;
2. deploy.yml на этом lock доходит до deploy-prod (не skipped).
