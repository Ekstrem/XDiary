# 2. Aggregate Design Canvas — Record

Канонический канвас: [`03-ddd/BC-Journal/02-aggregate-canvas.md`](../../../03-ddd/BC-Journal/02-aggregate-canvas.md)

- **Агрегат:** Record (единственный агрегат контекста).
- **Value Objects:** Root (startedAt, durationMin), Partner, Place, Positions, Tags,
  Costumes, Toys, Fantasies, Contraception, Orgasms, Origin.
- **Handled Commands:** CreateRecord, UpdateRecord, DeleteRecord, ImportRecord.
- **Created Events:** RecordCreated, RecordUpdated, RecordDeleted, RecordImported.
- **Инварианты (metadata DSL):** IsNewEntity (create), IsNotNewEntity + VOExists:Root
  (update/delete), PropertyNotNull:Root.StartedAt (create).
- **Инварианты (в handler'е, т.к. DSL не выражает диапазоны):** durationMin ≥ 1,
  ratings ∈ 0..5.

Метаданные агрегата заданы кодом в `Journal.Domain/Metadata/JournalMetadata.cs`
(эквивалент `seed_journal_metadata.sql`).
