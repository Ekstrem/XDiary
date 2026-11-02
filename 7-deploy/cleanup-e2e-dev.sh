#!/usr/bin/env bash
# cleanup-e2e-dev.sh — убрать накопленные E2E-данные с dev-стенда (DO).
#
# Зачем: каждый прогон front-e2e создаёт партнёра (POST /api/v1/partners {name}) и клиента
# через API, и НЕ удаляет их. К 02.11 на dev накопилось: clients 1748 (все с телефоном +7999…),
# partners 2635 (имена TE-<ts> и Ee2e-auto-<ts>). `#onboarding-list` в браузере отрисовывал
# 2607 узлов / 588 КБ HTML — из-за этого `.first()` в спеке выбирал произвольную карточку,
# а «Список партнёров пуст» появлялся даже когда партнёр создан. Это флейк стенда, не продукта.
#
# Безопасность:
#   * по умолчанию DRY-RUN: только считает и печатает;
#   * партнёров удаляет ТОЛЬКО по имени, которое генерируют спеки: TE-<число> или *e2e*;
#   * клиентов трогает только с флагом --clients (телефон +7999…), потому что клиенты
#     могут быть связаны с журналом и другими сервисами;
#   * --apply обязателен для любого удаления; печатает остаток после удаления;
#   * без dblink: запросы идут в каждую БД отдельно (расширения dblink на стенде нет).
#
# Использование:
#   bash /opt/xdiary/7-deploy/cleanup-e2e-dev.sh                 # посчитать
#   bash /opt/xdiary/7-deploy/cleanup-e2e-dev.sh --apply         # удалить партнёров
#   bash /opt/xdiary/7-deploy/cleanup-e2e-dev.sh --apply --clients   # + тестовых клиентов
set -euo pipefail

APPLY=0
CLIENTS=0
for arg in "$@"; do
  case "$arg" in
    --apply)   APPLY=1 ;;
    --clients) CLIENTS=1 ;;
    *) echo "неизвестный аргумент: $arg" >&2; exit 2 ;;
  esac
done

P() { sudo podman exec -i xdiary-pg psql -U postgres -d partners -v ON_ERROR_STOP=1 -t -A -c "$1"; }
C() { sudo podman exec -i xdiary-pg psql -U postgres -d client   -v ON_ERROR_STOP=1 -t -A -c "$1"; }

PARTNER_PATTERN="(\"Name\" LIKE 'TE-%' OR lower(\"Name\") LIKE '%e2e%')"
CLIENT_PATTERN="(\"Phone\" LIKE '+7999%')"

echo "dev: накопленные E2E-данные"
echo "  partners всего:        $(P 'SELECT count(*) FROM partners;')"
echo "  partners по шаблону:   $(P "SELECT count(*) FROM partners WHERE ${PARTNER_PATTERN};")  (TE-<ts> | *e2e*)"
echo "  clients всего:         $(C 'SELECT count(*) FROM clients;')"
echo "  clients по телефону:   $(C "SELECT count(*) FROM clients WHERE ${CLIENT_PATTERN};")  (+7999…)"

if [[ "$APPLY" -ne 1 ]]; then
  echo
  echo "DRY-RUN: ничего не удалено."
  echo "  удалить партнёров:              --apply"
  echo "  удалить партнёров и клиентов:   --apply --clients"
  exit 0
fi

echo
echo "Удаляю партнёров по шаблону…"
P "DELETE FROM partners WHERE ${PARTNER_PATTERN};" >/dev/null
echo "  осталось partners: $(P 'SELECT count(*) FROM partners;')"

if [[ "$CLIENTS" -eq 1 ]]; then
  echo "Удаляю тестовых клиентов (+7999…)…"
  C "DELETE FROM clients WHERE ${CLIENT_PATTERN};" >/dev/null
  echo "  осталось clients: $(C 'SELECT count(*) FROM clients;')"
else
  echo "  клиентов не трогаю (нужен --clients)."
fi

echo "готово."
