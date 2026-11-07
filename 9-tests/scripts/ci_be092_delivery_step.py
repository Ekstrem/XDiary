#!/usr/bin/env python3
"""ci_be092_delivery_step.py — шаг CI для приёмки `BE-092` без ручного прогона.

Зачем обёртка, а если есть `check_be092_delivery.py`:
    GitHub Actions валит шаг на любом non-zero коде, а приёмка различает три исхода:
    `0` — три строки совпали; `1` — расхождение поведения (вердикт продукту/выкатке,
    шаг обязан краснеть); `2` — не измерено (нет `--base`, стенд недоступен, сеть/SSL —
    «не дошло ни одного запроса»). Без обёртки `2` либо красит сборку там, где мерить
    было нечего, либо (через `|| true`) скрывает и настоящий `1`.

Правило (решение @TL `08:44`/`08:52`, семантика в `5-tech-backlog/tester/TE-096.md`):
    `0` → шаг зелёный; `1` → шаг красный; `2` → шаг **зелёный** с `::warning::`, кроме
    режима `--require-measured` (там `2` — красный: контур обязан быть доступен).

Использование в `deploy.yml` (зона @DO), после шага деплоя dev:
    - name: Приёмка BE-092 на dev (delivery/409/429)
      run: python 9-tests/scripts/ci_be092_delivery_step.py --base ${{ env.XDIARY_E2E_BASE_URL }}
      env:
        XDIARY_E2E_BASE_URL: https://xdiary-vps2.taile0b6ee.ts.net

    # на dev с TESTOTP ожидаем `1` по строке `delivery` (поля нет по замыслу):
    # либо запускать там, где канал реальный, либо ставить `continue-on-error: true`
    # с условием снятия «`delivery` появилось в теле» (§8). Значение по умолчанию —
    # строгий `1` = красный, чтобы «ожидаемый красный» не стал фоном.

Токен не требуется: номер генерируется в тестовом диапазоне `+7999…` и убирается
штабной очисткой стенда.
"""
import argparse
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
INNER = os.path.join(HERE, "check_be092_delivery.py")


def map_rc(inner_rc, require_measured, inner_out=""):
    """Возвращает (rc_шага, строка_заголовка). Чистая функция — её и проверяет --selftest."""
    if inner_rc == 0:
        return 0, "::notice::приёмка BE-092: 3/3 — все три строки совпали"
    if inner_rc == 1:
        return 1, "::error::приёмка BE-092: расхождение поведения — шаг валится (см. строки выше)"
    if require_measured:
        return 2, "::error::приёмка BE-092: не измерено, а контур обязателен (--require-measured)"
    return 0, "::warning::приёмка BE-092: не измерено (стенд недоступен / нет --base) — шаг НЕ валится: это конфигурация или сеть, а не продукт"


def run(base, require_measured, expect_no_delivery=False):
    if not base:
        rc, head = map_rc(2, require_measured)
        print("::warning::ci_be092_delivery_step: не задан --base — не измерено (это конфигурация шага, а не продукт)")
        print(head)
        print("квитанция шага: измерено: нет | контур не задан | rc=%d" % rc)
        return rc

    cmd = [sys.executable, INNER, "--base", base]
    if expect_no_delivery:
        cmd.append("--expect-no-delivery")
    proc = subprocess.run(
        cmd,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    out = (proc.stdout or "") + (proc.stderr or "")
    if out.strip():
        print(out.rstrip())

    inner_rc = proc.returncode if proc.returncode in (0, 1, 2) else 2
    rc, head = map_rc(inner_rc, require_measured)
    print(head)
    print(
        "квитанция шага: контур %s | код приёмки %d | код шага %d"
        % (base, inner_rc, rc)
    )
    return rc


CASES = []


def case(name, got, want, must=()):
    ok = got[0] == want and all(m in got[1] for m in must)
    CASES.append((name, ok, got, want, must))


def selftest():
    case("0 -> шаг зелёный", map_rc(0, False), 0, ["3/3"])
    case("1 -> шаг красный", map_rc(1, False), 1, ["расхождение", "валится"])
    case("2 -> шаг зелёный с warning", map_rc(2, False), 0, ["::warning::", "не измерено"])
    case("2 + --require-measured -> красный", map_rc(2, True), 2, ["::error::", "обязателен"])
    case("неизвестный код трактуется как «не измерено»", map_rc(7, False), 0, ["не измерено"])

    ok = sum(1 for _, good, *_ in CASES if good)
    for name, good, got, want, must in CASES:
        if not good:
            print("  [FAIL] %s: got rc=%s head=%r (ждали rc=%s, в тексте: %s)"
                  % (name, got[0], got[1][:80], want, ", ".join(must)))
        else:
            print("  [OK]   %s" % name)
    print("ci_be092_delivery_step --selftest: %d/%d (0/2 не валят шаг, 1 валит, --require-measured делает 2 красным)"
          % (ok, len(CASES)))
    return 0 if ok == len(CASES) else 1


def main():
    ap = argparse.ArgumentParser(description="Шаг CI для приёмки BE-092 (0/1/2 -> код шага)")
    ap.add_argument("--base", help="адрес API (dev/prod), напр. https://xdiary-vps2.taile0b6ee.ts.net")
    ap.add_argument("--require-measured", action="store_true",
                    help="делать «не измерено» (2) красным: для контура, который обязан быть доступен")
    ap.add_argument("--expect-no-delivery", action="store_true",
                    help="тестовый режим (TESTOTP): пробрасывается в приёмку — `delivery` в теле нет по замыслу")
    ap.add_argument("--selftest", action="store_true")
    args = ap.parse_args()
    if args.selftest:
        return selftest()
    return run(args.base, args.require_measured, args.expect_no_delivery)


if __name__ == "__main__":
    sys.exit(main())
