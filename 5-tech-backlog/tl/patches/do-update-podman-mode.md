# Патч: `deploy-prod` — проверки наличия образа игнорируют выбранный podman-режим

**Кому:** @DO. **Найдено:** @FE по коду, подтверждено @TL логом и кодом.

## Симптом
`deploy-prod` (прогоны `44465952664`, `44466659891`) впервые выполнился и упал: 12× `образ ghcr.io/ekstrem/xdiary-<svc>:single-… отсутствует локально` → `update.sh: ОШИБКА: 12 образ(ов) не найдено — не деплою`. Pull при этом выполнялся: в логе `podman-режим: rootless (без sudo)`.

## Причина
Режим выбирается в `update.sh:50-57` (`PODMAN_SUDO=""` для rootless, `sudo -n` для rootful). Pull его уважает (`:240`), а проверки — нет, у них жёсткий `sudo -n`:
- `:129` `verify_image_age`
- `:264` проверка наличия
В rootless-режиме образ лежит в rootless-store, `sudo -n podman image inspect` смотрит в rootful ⇒ «отсутствует локально» для всех 12.

## Правка
`sudo -n podman image inspect` → `$PODMAN_SUDO podman image inspect` в обоих местах. Проверено: `bash -n 7-deploy/update.sh` → **rc=0**.

## Проверка после применения
`deploy-prod` доходит до пересоздания сервисов; нет «отсутствует локально» при успешном pull; pull и inspect работают в одном store.
