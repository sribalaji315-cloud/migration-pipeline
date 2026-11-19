-- Step 1 (BOM-scoped import): previous item headers outside the scope + fresh headers for scoped items.
-- Requires table scope_item(item_id) in the session.
COPY (
    SELECT *
    FROM read_parquet('{{previous_dir}}/items.parquet')
    WHERE item_id NOT IN (SELECT item_id FROM scope_item)
    UNION ALL BY NAME
    SELECT * FROM read_parquet('{{delta_path}}')
    ORDER BY item_id, _row_hash
) TO '{{bronze_dir}}/items.parquet' (FORMAT PARQUET, COMPRESSION ZSTD);
