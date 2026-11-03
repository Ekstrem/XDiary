# `openapi/` — реестр контрактов (BE-050, инкремент 1)

Источник правды контрактов для APIM (ADR-023). Владелец артефакта — **@BE**;
наполнение с dev выполнил **@DO** по явному разрешению @TL (2026-11-09 22:30).

## Что снято

| Файл | Сервис | Путей | Ревизия образа | Статус |
|---|---|---|---|---|
| `client.json` | `Client.Api` | 7 | `176c5f15446e` | снято |
| `analytics.json` | `Analytics Microservice API` | 8 | `cd83126ea1ee` | снято |

Остальные 10 сервисов **не отдают спеку** (см. «Почему не 12»).

## Как снято (воспроизводимо)

Стенд: **dev** (`xdiary-vps2`), не прод. Контейнеры dev под rootful podman, порты не
опубликованы, имена в CNI-DNS не резолвятся, `curl`/`wget` внутри контейнеров нет —
поэтому берём IP контейнера с хоста:

```bash
ip=$(sudo -n podman inspect xdiary-<service> --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
curl -s "http://$ip:8080/swagger/v1/swagger.json"
sudo -n podman inspect xdiary-<service> --format '{{index .Config.Labels "org.opencontainers.image.revision"}}'
```

Дата снятия: **2026-11-09**, ревизии образов зафиксированы в таблице выше (по метке
`org.opencontainers.image.revision`), чтобы реестр можно было сопоставить с коммитом.

## Почему не 12 (блокер, а не «не смог»)

В коде swagger подключён условно:

```csharp
// 6-msa/BC-Client/src/Client.Api/Program.cs:52
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
```

Так — почти во всех сервисах; **`Analytics` единственный**, у кого условие
`IsDevelopment() || EnvironmentName == "Single"` (`BC-Analytics/src/Analytics.Api/Program.cs:27`).
Стенд dev работает с `ASPNETCORE_ENVIRONMENT=Single`, поэтому у 10 сервисов
`/swagger/v1/swagger.json` = **404**, а `api-gateway` за авторизацией даёт **401**
(и 401 на несуществующих путях тоже — по нему нельзя судить о наличии маршрута).

**Что нужно, чтобы получить все 12** (зона @BE, одна строка на сервис):

1. заменить условие на флаг конфигурации, например `if (app.Configuration.GetValue<bool>("Swagger:Enabled"))`
   и включать его переменной окружения `Swagger__Enabled=true` на dev (в CI — по умолчанию);
2. **или** повторить условие `Analytics`: `IsDevelopment() || EnvironmentName == "Single"`.

После деплоя образа на dev реестр снимается одной командой (см. выше) — правки
`check_edm`/drift-check не нужны, путь реестра передаётся параметром.

## Правила для этого каталога

- Файлы — **снимок с работающего сервиса**, не ручная правка: правка руками = дрейф.
- Правка контракта = правка кода сервиса, затем повторное снятие (инкремент 2 —
  генерация в CI на сборке, чтобы реестр не уставал).
- `_meta.json` — служебный: ревизия образа и число путей на момент снятия.
