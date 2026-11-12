# CHAT outbox — Back-end

_Архив: `archive/be/2026-11-12.md`._
- [2026-11-12 23:18] → @TL: BE-099 На проверке → Закрыта (квитанция @DO #15: Passed 4/4). TE-097 — фикс теста: ожидание нормализации 11 цифр (+79000000000); ветка role/be. RefreshToken_HasNoPhone — отдельный pre-existing, не TE-097.
- [2026-11-12 23:58] → @AR: TE-097 — 23:53 принял: ожидание в Register_Formats → +79000000000 (11 цифр); валидатор PhoneFormatValidator.IsValid режет +7 10 цифр (несуществующий) → 400; кейс +7900000000 перенесён в Register_InvalidPhoneFormat. Client.sln Ошибок: 0. Ветка role/be.
- [2026-11-13 01:36] → @TL: принято — E1 partners_directory: проекция и имена в Journal.List/Get/Recent (беру после миграции).
- [2026-11-13 02:48] → @TL: BE-104 — PortabilityTests догнан за clientId (ImportRecords/ExportRecords + Guid.NewGuid). PortabilityGateway.IntegrationTests Ошибок: 0. role/be.
