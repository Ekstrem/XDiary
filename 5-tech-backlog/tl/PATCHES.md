# PATCHES: четыре правки, которые разблокируют `deploy-prod` (@TL, 2026-11-05 22:45)

Собрано, чтобы каждая правка занимала **минуту** и не требовала перечитывать чат.
Каждая — в **своей** зоне; @TL ничего из этого не применяет.

---

## 1. `deploy.yml` → `smoke-api`: мёртвый путь вместо измерения  (зона @DO)

**Сейчас** (`deploy.yml:127-138`): шаг зовёт `9-tests/E2E/E2E.Tests/E2E.IntegrationTests.csproj`, которого **нет** ⇒ гард делает `exit 2` ⇒ job `smoke-api` = failure ⇒ `deploy-prod` **skipped**.

**Заменить тело шага на измеряемое:**

```yaml
      - name: API-канон входа (measure, не заглушка)
        run: |
          python 9-tests/scripts/check_apim_auth.py --all --base "$XDIARY_E2E_BASE_URL"
```

`check_apim_auth --all` даёт `(ii-a)` 74/74 · `(ii-c)` 2/2 · `via=gravitee` · `rc=0` — это **настоящая** проверка API на стенде.
`rc=2` («не измерено») остаётся `rc=2`, а не зелёным: правило «красное значит баг» не ослабляем.

---

## 2. `Analytics.Api/Program.cs`: нет `/health`  (зона @BE, `BE-081`)

У **11** сервисов есть, у Analytics — **нет** (вхождений `MapHealthChecks` в `BC-Analytics` = **0**), поэтому `/health` уходит в авторизацию и даёт **401**.

```csharp
// 6-msa/BC-Analytics/src/Analytics.Api/Program.cs — как в BC-Catalog:38
builder.Services.AddHealthChecks();        // если ещё нет
...
app.MapHealthChecks("/health").AllowAnonymous();
```

Критерий: `analytics /health` → **200** без токена; все 12 сервисов → 200.

---

## 3. `8-front/js/repository.js`: `putPartner` не должен требовать `profile` для дефолтов  (зона @FE, `FE-045`)

Дефолты формы: `photo=""`, `gender="female"`, `notes=""` — то есть `profile` после `register` **ничего не меняет**.

```js
// repository.js:182-186 — было: register → profile → return
return Api.partners.register(p.name).then(function (m) {
  var id = m.id;
  var needProfile = !!p.photo || (p.gender && p.gender !== "female") || !!p.notes;
  if (!needProfile) { p.id = id; return p; }
  return Api.partners.profile(id, { photo: p.photo || "", gender: p.gender || "female", notes: p.notes || "" })
    .then(function () { p.id = id; return p; });
});
```

Плюс **один** retry (200 мс) для `profile`/`link-client` на 400/404 с логом `retry after lag`; к `register`/`remove` — **не** применять.

---

## 4. Перепрогон контрактных тестов  (зона @DO/CI, `BE-080`)

```bash
XDIARY_E2E_BASE_URL=https://xdiary-vps2.taile0b6ee.ts.net   dotnet test 6-msa/BC-Journal/src/Journal.ApiTests --filter PartnerSoftDelete
```

Ожидание после правок контроллера (`[Authorize]` + сверка `sub` + `NotFound`/`Forbid`): **`Failed: 0`**.
В `## Результат` — **оба** числа (`Failed`/`Passed`) и `rc` от `dotnet`, а не от `tail`.

---

## Почему именно эти четыре

`deploy-prod` требует `smoke-api == success` **и** (`smoke-front == success` **или** фронт не менялся).
Сейчас красные оба ⇒ прод стоит с **19:13**. Пункты 1–3 убирают причины красного, пункт 4 подтверждает, что
`BE-080` закрыт по факту, а не по диффу.
