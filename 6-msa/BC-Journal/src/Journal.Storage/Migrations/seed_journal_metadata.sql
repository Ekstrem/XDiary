-- ============================================================================
-- Seed: Record aggregate metadata for AbstractAggregate (документация).
-- Схема: abstract_aggregate
--
-- ВАЖНО: для профиля Single микросервис journal использует in-memory метаданные
-- (JournalMetadata.Build() в коде) — этот SQL приведён для справки и для
-- будущего перехода на Postgres-хранилище метаданных (AbstractAggregate.Postgres).
-- ============================================================================

-- 1. Aggregate
INSERT INTO abstract_aggregate.aggregates (id, aggregate_name, description, created_at, updated_at)
VALUES (
    'a0000000-0000-0000-0000-000000000001',
    'Record',
    'Агрегат Record: фиксация одного акта пары.',
    NOW(), NOW()
)
ON CONFLICT (aggregate_name) DO NOTHING;

-- 2. Value Objects (11 VOs)
INSERT INTO abstract_aggregate.value_objects (id, aggregate_id, name, is_aggregate_root, is_collection) VALUES
    ('b0000000-0000-0000-0000-000000000001', 'a0000000-0000-0000-0000-000000000001', 'Root', true, false),
    ('b0000000-0000-0000-0000-000000000002', 'a0000000-0000-0000-0000-000000000001', 'Partner', false, false),
    ('b0000000-0000-0000-0000-000000000003', 'a0000000-0000-0000-0000-000000000001', 'Place', false, false),
    ('b0000000-0000-0000-0000-000000000004', 'a0000000-0000-0000-0000-000000000001', 'Positions', false, false),
    ('b0000000-0000-0000-0000-000000000005', 'a0000000-0000-0000-0000-000000000001', 'Tags', false, false),
    ('b0000000-0000-0000-0000-000000000006', 'a0000000-0000-0000-0000-000000000001', 'Costumes', false, false),
    ('b0000000-0000-0000-0000-000000000007', 'a0000000-0000-0000-0000-000000000001', 'Toys', false, false),
    ('b0000000-0000-0000-0000-000000000008', 'a0000000-0000-0000-0000-000000000001', 'Fantasies', false, false),
    ('b0000000-0000-0000-0000-000000000009', 'a0000000-0000-0000-0000-000000000001', 'Contraception', false, false),
    ('b0000000-0000-0000-0000-00000000000a', 'a0000000-0000-0000-0000-000000000001', 'Orgasms', false, false),
    ('b0000000-0000-0000-0000-00000000000b', 'a0000000-0000-0000-0000-000000000001', 'Origin', false, false)
ON CONFLICT (aggregate_id, name) DO NOTHING;

-- 3. Operations (4 бизнес-операции)
INSERT INTO abstract_aggregate.operations (id, aggregate_id, command_name, description, merge_strategy) VALUES
    ('d0000000-0000-0000-0000-000000000001', 'a0000000-0000-0000-0000-000000000001', 'CreateRecord', 'Создать запись', 'FullReplace'),
    ('d0000000-0000-0000-0000-000000000002', 'a0000000-0000-0000-0000-000000000001', 'UpdateRecord', 'Отредактировать запись', 'FullReplace'),
    ('d0000000-0000-0000-0000-000000000003', 'a0000000-0000-0000-0000-000000000001', 'DeleteRecord', 'Удалить запись', 'FullReplace'),
    ('d0000000-0000-0000-0000-000000000004', 'a0000000-0000-0000-0000-000000000001', 'ImportRecord', 'Импортировать запись', 'FullReplace')
ON CONFLICT (aggregate_id, command_name) DO NOTHING;
