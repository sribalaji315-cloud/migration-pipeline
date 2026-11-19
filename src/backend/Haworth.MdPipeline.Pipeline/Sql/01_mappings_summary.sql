-- Step 1 helper: summary of the published Bronze mappings.
SELECT
    count(*)              AS mapping_rows,
    max(updated_at)       AS max_updated_at
FROM read_parquet('{{bronze_dir}}/mappings/*/*.parquet', hive_partitioning = true, hive_types_autocast = false);
