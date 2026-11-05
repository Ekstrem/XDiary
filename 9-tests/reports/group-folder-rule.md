# Правило «группа без csproj на своём уровне» — спецификация для `tools/check_test_isolation.py`

**Кому:** @DO (владелец `tools/`), **зачем:** `BE-070` не закрывается, а гейт зелёный.
**Статус документа:** спецификация проверки, не код. Код правит @DO (зона `tools/`).
**Основание:** `ADR-028` §3.0.2/§3.0.3/§3.1(е) — «`6-msa/ApiGateway.Tests/` — **ГРУППА**,
на этом уровне csproj **НЕТ**; в папке проекта — ровно один csproj на проект».
Замер дефекта: `2026-11-05`, `origin` = `2a3c449b`.

## 1. Факт, который сейчас не проверяется

```
6-msa/ApiGateway.Tests/
  ApiGateway.Tests.csproj              ← csproj на уровне ГРУППЫ (запрещено)
  Gateways.IntegrationTests.csproj     ← второй (запрещено)
  GatewaysApi.IntegrationTests.csproj  ← третий (запрещено)
  AppleHealthGatewayTests.cs           ← остаток «копии» вместо переноса
  GarminGatewayTests.cs
  GatewayBehaviorTests.cs
  GatewayRoutesTests.cs
  StandFactAttribute.cs
```
Итог: **3 csproj + 5 `.cs`** в корне группы; `check_test_isolation` → `rc=0`, «нарушений 0».
Причина по коду гейта: (11) ловит только **вложенные** csproj (внутри другого csproj-проекта),
а здесь они **сиблинги**; правила «на уровне группы csproj = 0» нет вовсе.

## 2. Что добавить (псевдокод)

```python
GROUP_DIRS = ("6-msa/ApiGateway.Tests",)   # группы по ADR-028 §3.0.2 (список расширяемый)

def scan_group_dirs(root):
    out = []
    for group in GROUP_DIRS:
        gdir = os.path.join(root, group)
        if not os.path.isdir(gdir):
            continue
        # (а) на верхнем уровне группы csproj быть не должно
        top = [fn for fn in sorted(os.listdir(gdir))
               if fn.endswith(".csproj") and os.path.isfile(os.path.join(gdir, fn))]
        for fn in top:
            out.append(("12", f"{group}/{fn}",
                        "csproj на уровне ГРУППЫ: проект должен лежать в подпапке "
                        "<Имя>/<Имя>.csproj (ADR-028 §3.0.2)"))
        # (б) каждый подпроект — ровно один csproj внутри СВОЕЙ подпапки
        for d in sorted(os.listdir(gdir)):
            sub = os.path.join(gdir, d)
            if not os.path.isdir(sub):
                continue
            csprojs = [fn for fn in sorted(os.listdir(sub)) if fn.endswith(".csproj")]
            if not csprojs:
                out.append(("12", f"{group}/{d}",
                            "подпапка группы без csproj: либо проект, либо удалить"))
            elif len(csprojs) > 1:
                out.append(("12", f"{group}/{d}",
                            "несколько csproj в одной папке: %s (ровно один на проект)"
                            % ", ".join(csprojs)))
    return out
```

В `scan()` вызывать после существующих проверок; в итоговый `v` складывать как остальные.

## 3. Два обязательных изменения отчётности (по замечанию @TL `13:30`)

1. **`0 найденных csproj` → `rc=2` «НЕ ИЗМЕРЕНО»**, а не `0`: сейчас `--root 6-msa`
   обходит `6-msa/6-msa` (0 файлов) и печатает «изоляция в норме». Текст:
   `НЕ ИЗМЕРЕНО: найдено 0 csproj под <root>/6-msa — проверять нечего`.
2. **Печатать два числа**, а не одно: `проверено проектов: N | нарушений: M`.
   Сейчас `нарушений 0 (0 проектов)` читается как «проектов нет» — на этом уже
   ошиблись @TL (дважды) и я (один раз).

## 4. Кейсы `--selftest` (дефект + норма)

| Кейс | Ожидание |
|---|---|
| группа с `ApiGateway.Tests.csproj` на верхнем уровне | **1** нарушение, категория (12) |
| группа, где проект в подпапке `ApiGateway.ApiTests/ApiGateway.ApiTests.csproj` | **0** |
| подпапка с двумя csproj | **1** нарушение (12) |
| подпапка без csproj | **1** нарушение (12) |
| пустой корень (0 csproj) | **`rc=2`**, не `0` |

## 5. Как проверить после правки

```bash
python tools/check_test_isolation.py --selftest          # кейсы выше → все зелёные
python tools/check_test_isolation.py                     # на текущем origin → 3 нарушения (12)
git ls-tree -r --name-only origin/main -- 6-msa/ApiGateway.Tests | grep -c '\.csproj$'   # = 1 после @BE
```

## 6. Реализовано @DO — и проверено дефектом (2026-11-05, REF `2140208a`)

Спецификация выполнена в `tools/check_test_isolation.py`: правило **(12)** и **два числа** в выводе,
`--selftest` **18/18**. Моя независимая проверка — красные контроли во временных деревьях
(гейт не пишет в репозиторий, поэтому дефект строится в `%TEMP%`):

| Что подложено | Ожидание | Факт |
|---|---|---|
`6-msa/ApiGateway.Tests/ApiGateway.Tests.csproj` + `Gateways.IntegrationTests.csproj` на уровне группы | `rc=1`, (12) ×2 | **`rc=1`**, `::error::(12)` ×2, «проверено: 2 / нарушений: 2» |
одна подпапка с **двумя** csproj | `rc=1` | **`rc=1`**, `::error::(12)` ×2 (по одному на файл) |
правильная раскладка (один csproj в своей подпапке) | `rc=0` | **`rc=0`**, «проверено: 1 / нарушений: 0» |
пустой корень (0 csproj) | `rc=2` | **`rc=2`**, «0 csproj, проверять нечего» |

То есть гейт **краснеет на нашем дефекте** и **зеленеет на норме** — «подключён ≠ написан» закрыто
для этого правила: два прогона + `--selftest` внутри.

## 7. Решение @TL против `--warn-only` (для истории)

@TL `13:50`: «красный сейчас означал **реальный дефект**, и „мягкий режим“ сделал бы его жёлтым,
который проигнорируют… лечить надо **проверку**, а не ослаблять её». Записано, потому что я
предлагал `--warn-only` — и это было бы ослаблением. Правильный порядок: сначала правило и
счёт проектов, потом (если понадобится) мягкий режим — но не вместо.

## 8. Вторая находка того же разбора: `EnableDefaultCompileItems=false` (в ДВУХ сборках)

Замер по `origin` (`REF=1f522807`): хак стоит **в обоих** csproj группы, а не в одном:

| csproj | `EnableDefaultCompileItems=false` | `Compile Include` | `.cs` в папке |
|---|---|---|---|
`ApiGateway.Tests/ApiGateway.Tests/ApiGateway.Tests.csproj` | **да** | `GatewayRoutesTests.cs` | 1 |
`ApiGateway.Tests/ApiGateway.ApiTests/ApiGateway.ApiTests.csproj` | **да** | `GatewayBehaviorTests.cs`, `StandFactAttribute.cs` | 2 |

Сейчас списки **полные** (ни один `.cs` не забыт), поэтому «сегодня» покрытие не потеряно. Опасность —
на будущее, и она **проверена дефектом**: копируем `ApiGateway.Tests.csproj`, кладём рядом новый
`NewFeatureTests.cs` → `Compile Include` его не содержит, `EnableDefaultCompileItems=false` запрещает
глоб ⇒ **`NewFeatureTests.cs` не попадёт в сборку**, тест не запустится, CI останется зелёным.
Класс: «тест есть, но его нет» — тише, чем «теста нет».

**Почему хак больше не нужен:** он появился, когда `csproj` лежал на уровень выше своих `.cs`
(глоб тянул файлы соседнего проекта). После раскладки «подпапка = имя проекта» каждый `.cs` лежит
рядом со своим `csproj`, и дефолтный глоб даёт ровно нужный набор.

**Что должно проверяться (правило для `tools/`, зона @DO):** если в тестовом csproj стоит
`EnableDefaultCompileItems=false`, то множество `<Compile Include>` обязано **точно совпадать** с
множеством `.cs` в папке проекта. Расхождение (файл есть, но не перечислен) → `rc=1` с именем файла.
Это ловит и «забыли добавить», и «удалили файл, а строку оставили».

**Команда проверки сейчас:**
```bash
git ls-tree -r --name-only origin/main -- 6-msa/ApiGateway.Tests | grep -c '\.csproj$'        # = 2
python - <<'PY'
# сверка Compile Include с файлами в папке (без сборки)
PY
```
