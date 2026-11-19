-- Step 1 helper (Swing-only import): row count of the carried-forward Migration Tool item headers.
SELECT count(*) AS item_rows
FROM read_parquet('{{bronze_dir}}/items.parquet');
