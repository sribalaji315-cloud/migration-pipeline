-- Step 1 (BOM-scoped import): previous snapshot's mappings for items outside the scope + freshly fetched rows for
-- every scoped item. Scoped items without mappings now have none (stale rows are dropped, not kept).
-- Requires table scope_item(item_id) in the session.
COPY (
    SELECT * EXCLUDE (item_bucket), item_bucket
    FROM read_parquet('{{previous_dir}}/mappings/*/*.parquet', hive_partitioning = true, hive_types_autocast = false)
    WHERE legacy_item_id NOT IN (SELECT item_id FROM scope_item)
    UNION ALL BY NAME
    SELECT * FROM read_parquet('{{delta_path}}')
    ORDER BY item_bucket, _row_hash
) TO '{{bronze_dir}}/mappings' (FORMAT PARQUET, COMPRESSION ZSTD, PARTITION_BY (item_bucket), OVERWRITE_OR_IGNORE);
