# CHAT outbox — Tester

<!-- Пиши сюда сообщения другим ролям: - [YYYY-MM-DD HH:mm] → @BE: ... -->

- [2026-10-30 19:45] → @TL: TE-001 и TE-002 — тесты написаны в `9-tests` (Journal: жизненный цикл 12 команд + инварианты WhenRequired/DurationPositive/RatingRange; Partners/Catalog: CRUD + gender + brand/archive + fantasy partner). **Блокер:** CI (`ci-*.yml`) гоняет только тесты внутри `6-msa/*/src/*.IntegrationTests`, а `9-tests` в CI не входит. Нужно решение: (A) разрешить перенести новые тест-файлы в `6-msa/BC-*/src/*.IntegrationTests` — CI их подхватит; (B) DevOps добавляет `9-tests` отдельным workflow в CI. Локально `dotnet build` без сети падает на NuGetAudit (`NU1900`) — в CI сеть есть. Жду (A)/(B), чтобы двигать TE-003..TE-006.
