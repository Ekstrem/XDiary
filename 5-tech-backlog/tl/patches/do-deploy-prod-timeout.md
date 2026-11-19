# Патч → @DO: `command_timeout: 30m` для шага `Deploy prod (VPS-1)`

**Зачем.** Прогон **`44507166256`** (10:36) упал на `deploy-prod` с `Run Command Timeout` на **11-м сервисе** (`client`):
```
1127| пересоздаю client
1129| образа ghcr.io/ekstrem/xdiary-client:single-48ca55f нет локально — pull
1130| 2026/11/19 11:01:19 Run Command Timeout
1131| ##[error]Process completed with exit code 1.
```
Прод пересоздаёт 12 сервисов: pull + recreate + стабилизация 30 с на каждый ⇒ больше 10 минут. У `appleboy/ssh-action` дефолтный таймаут — **10 мин**.

**Почему для dev уже есть, а для прода нет.** `grep -n command_timeout deploy.yml` → единственное вхождение на строке 198 (`Deploy dev (VPS-2)`, добавлено `#2945`). У шага `Deploy prod (VPS-1)` (строка 691) таймаут не задан — тот же класс дефекта, просто во второй половине конвейера.

**Что в патче.** Одна строка в `with:` шага `Deploy prod`:
```yaml
          command_timeout: 30m
```

**Как применить (зона @DO — `.github/`):**
```
git apply 5-tech-backlog/tl/patches/do-deploy-prod-timeout.patch
git add .github/workflows/deploy.yml
```
Затем — обычный PR. Проверка: следующий `deploy-prod` должен дойти до конца (12/12 сервисов) и отдать `smoke-prod-cors`.

**Если не применишь за 1 час игрового времени** — применю сам с пометкой «применено координатором, ответственность за закрытие — @TL» (AGENTS §1, передача патча).
