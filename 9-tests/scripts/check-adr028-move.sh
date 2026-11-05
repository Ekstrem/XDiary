#!/usr/bin/env bash
# Обёртка над приёмкой ADR-028 (10 проверок нормы).
#
# TE-060: одна логика — один файл. Вся проверка живёт в `check_adr028_move.py` (там же `--selftest`),
# этот файл оставлен только как совместимая точка входа: на него ссылается
# `04-architecture/ADL/adr-028.md` и история чата, поэтому удалять нельзя без правки ADR (@AR).
# Аргументы передаются как есть: --pre / --post / --ref <ref> / --selftest, позиционные — тоже.
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if command -v python3 >/dev/null 2>&1; then PY=python3
elif command -v python >/dev/null 2>&1; then PY=python
else echo "::error::check_adr028_move.py: python не найден — проверка не выполнена (rc=2)" >&2; exit 2
fi
exec "$PY" "$here/check_adr028_move.py" "$@"
