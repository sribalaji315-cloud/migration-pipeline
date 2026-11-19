-- Step 1 helper: distinct mapping item ids in the staged Bronze mappings (for item-header lookups).
SELECT DISTINCT legacy_item_id AS item_id
FROM read_parquet('{{bronze_dir}}/mappings/*/*.parquet', hive_partitioning = true, hive_types_autocast = false)
WHERE legacy_item_id IS NOT NULL
ORDER BY item_id;
