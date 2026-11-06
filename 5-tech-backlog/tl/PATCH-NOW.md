# PATCH-NOW — три правки, после которых MVP открывается пользователю

Состояние на 2026-11-06 12:30. Каждая правка — механическая, суммарно ~15 минут.
Порядок **важен**: (1) → (2) → (3), потому что сборка блокирует публикацию образов, а lock без образов ломает деплой.

---

## ① Сборка: вернуть удалённый `IValueObjectJsonConverter` (`BE-089`)

**Симптом (аннотация CI, `ci-partners`/`ci-journal`):**
```
The type or namespace name 'IValueObjectJsonConverter' could not be found
```

**Правка 1a — создать `6-msa/Shared/XDiary.Contracts/IValueObjectJsonConverter.cs`:**

```csharp
namespace XDiary.Contracts;
using System.Text.Json;
using System.Text.Json.Serialization;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

/// <summary>
/// BE-059/BE-089: полиморфная сериализация IValueObject ($type + value).
/// Читается по AssemblyQualifiedName; concrete-типы должны быть десериализуемы.
/// </summary>
public sealed class IValueObjectJsonConverter : JsonConverter<IValueObject>
{
    public override IValueObject Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        var typeName = root.GetProperty("$type").GetString();
        var type = Type.GetType(typeName!)
            ?? throw new InvalidOperationException($"тип VO не найден: {typeName}");
        return (IValueObject)JsonSerializer.Deserialize(root.GetProperty("value").GetRawText(), type, options)!;
    }

    public override void Write(Utf8JsonWriter writer, IValueObject value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("$type", value.GetType().AssemblyQualifiedName);
        writer.WritePropertyName("value");
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
        writer.WriteEndObject();
    }
}
```

**Правка 1b — удалить `using Partners.Storage.Outbox;` из `6-msa/BC-Partners/src/Partners.Api/Events/OutboxDispatcher.cs:10`**
(каталог `Partners.Storage/Outbox` после `8f053141` пуст, namespace больше не существует ⇒ это второй `CS0246`).

**Проверка:** `git grep "class IValueObjectJsonConverter"` → ровно одно; `ci-partners`/`ci-journal` → Build success; `docker` job — **выполняется**.

---

## ② Образы: сдвинуть `7-deploy/images.lock` на коммит с `BE-088`

**Только после ①** (иначе образов для нового тега просто нет).

```diff
-journal              single-8a65f30  6-msa/BC-Journal
-partners             single-6c2f850  6-msa/BC-Partners
+journal              single-e7cf387  6-msa/BC-Journal
+partners             single-e7cf387  6-msa/BC-Partners
-analytics            single-8a65f30  6-msa/BC-Analytics
+analytics            single-ab64fde  6-msa/BC-Analytics
```

`e7cf387` — коммит `BE-088` (гибрид `GetByIdAsync`: read-модель → пересборка из событий).
Пред-проверка: `podman manifest inspect ghcr.io/ekstrem/xdiary-partners:single-e7cf387` (и `…-journal:…`).

---

## ③ Вход в прод: CORS (`DO-106`)

**Симптом:** в приложении «Нет связи с сервером», хотя `POST /api/v1/clients` отвечает **200**.
**Причина:** `OPTIONS` (preflight) → **401**, в ответе нет `Access-Control-Allow-Origin`.

**Вариант A (правильный, APIM):** в Management API v2 (по образцу `7-deploy/apim/import.sh`):
прочитать API → `jq '.proxy.cors = {enabled:true, allowCredentials:false, allowOrigin:["https://ekstrem.github.io"], allowHeaders:["authorization","content-type","x-requested-with"], allowMethods:["GET","POST","PUT","DELETE","OPTIONS"], maxAge:600}'` → `PUT` → `POST …/deployments`. Применить ко **всем 12** API.

**Вариант B (hotfix Caddy, `Caddyfile.prod`):**

```caddy
@preflight method OPTIONS
respond @preflight 204 {
    header Access-Control-Allow-Origin "https://ekstrem.github.io"
    header Access-Control-Allow-Methods "GET, POST, PUT, DELETE, OPTIONS"
    header Access-Control-Allow-Headers "authorization, content-type"
    header Access-Control-Max-Age "600"
}
header Access-Control-Allow-Origin "https://ekstrem.github.io"
```

**Проверка:**
```bash
curl -s -o /dev/null -D - -X OPTIONS "https://xdiary-vps1.taile0b6ee.ts.net/api/v1/clients" \
  -H "Origin: https://ekstrem.github.io" \
  -H "Access-Control-Request-Method: POST" \
  -H "Access-Control-Request-Headers: content-type" | grep -i "access-control\|^HTTP"
```
Ждём `204/200` + `Access-Control-Allow-Origin`.

---

## ④ После ①–③ — что должно стать зелёным

| Что | Ожидание |
|---|---|
| `ci-partners`, `ci-journal` | Build success, `docker` публикует образы |
| Deploy | `smoke-api` success · `smoke-front` success · **`deploy-prod` выполняется** |
| Приложение на проде | вход по телефону проходит; партнёры/записи работают (`BE-088`) |
| `gen_images_lock --check` | `rc=0` |

## Что при этом ещё НЕ закрыто (не блокирует вход)

- тесты изоляции для `BE-084` (4 сервиса) — `BE-084` в `На проверке`, я его не принял;
- `ci-front.yml:50` `if: … != 'push'` — браузерных прогонов на push нет (`DO-103`);
- `XDIARY_CHANGE_PIN: '1'` (`DO-103`);
- `check_readstore_ddl` устарел (`DO-105`), `check_secrets` ложно краснеет на hex (`DO-102`).
