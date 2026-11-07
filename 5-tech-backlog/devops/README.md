# Папка DevOps-задач

> `$CREDS` ниже — **файл кредов стенда** (600, вне репозитория; путь знает координатор).
> В git имя файла кредов не пишем — §6: репозиторий публичный.


Здесь лежат карточки `DO-NNN.md`, отчётные документы и этот указатель.

## 🔑 Где искать доступы и креды

**Значения никогда не пишутся в задачи, чат и README** (репозиторий публичный, §6 `AGENTS.md`).
Они лежат в закрытом gitignore-каталоге:

| что | путь | в git |
|---|---|---|
| **APIM** (логин, пароль, URL, как проверить, как ротировать) | `5-tech-backlog/devops/secrets/APIM-credentials.md` | **нет** — закрыт правилом `.gitignore` |
| тот же доступ для инструментов | `.env.apim.local` в корне (переменная `GRAVITEE_BASIC`) | **нет** — правило `.env.*` |
| источник истины на проде | `$CREDS` (права 600) | — |
| доступ для CI | GitHub Secrets → `GRAVITEE_BASIC` (использует `apim-drift.yml`) | — |

Проверка, что каталог действительно закрыт:

```bash
git check-ignore -v 5-tech-backlog/devops/secrets/APIM-credentials.md
# ожидаемо: .gitignore:<строка>:5-tech-backlog/devops/secrets/
git status --short 5-tech-backlog/devops/secrets/    # пусто
```

Если нужен новый секрет: положить файл в `5-tech-backlog/devops/secrets/`, убедиться, что
`git check-ignore` его видит, и добавить строку в таблицу выше. **В git — только путь, никогда значение.**
