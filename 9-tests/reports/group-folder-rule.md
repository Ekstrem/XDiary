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
