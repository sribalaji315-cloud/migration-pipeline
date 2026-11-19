-- Step 1 incremental merge: previous Bronze mappings for unchanged items + the full delta row set for changed items.
-- Assumption: the bulk endpoint returns every current mapping row of an item whose mappings changed.
COPY (
    SELECT * EXCLUDE (item_bucket), item_bucket
    FROM read_parquet('{{previous_dir}}/mappings/*/*.parquet', hive_partitioning = true, hive_types_autocast = false)
    WHERE legacy_item_id NOT IN (
        SELECT DISTINCT legacy_item_id FROM read_parquet('{{delta_path}}')
    )
    UNION ALL BY NAME
    SELECT * FROM read_parquet('{{delta_path}}')
    ORDER BY item_bucket, _row_hash
) TO '{{bronze_dir}}/mappings' (FORMAT PARQUET, COMPRESSION ZSTD, PARTITION_BY (item_bucket), OVERWRITE_OR_IGNORE);
