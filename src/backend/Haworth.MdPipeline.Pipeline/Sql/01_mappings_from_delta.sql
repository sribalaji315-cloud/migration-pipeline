-- Step 1 (BOM-scoped import, no earlier mappings): partition the freshly fetched rows into the snapshot.
COPY (
    SELECT * FROM read_parquet('{{delta_path}}')
    ORDER BY item_bucket, _row_hash
) TO '{{bronze_dir}}/mappings' (FORMAT PARQUET, COMPRESSION ZSTD, PARTITION_BY (item_bucket), OVERWRITE_OR_IGNORE);
