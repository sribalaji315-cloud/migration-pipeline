-- Step 4 (part 2): Bronze mappings view (only when mapping Parquet files exist).
CREATE OR REPLACE VIEW mt_mappings AS
SELECT * FROM tpl_mt_mapping
UNION ALL BY NAME
SELECT * EXCLUDE (item_bucket)
FROM read_parquet('{{bronze_dir}}/mappings/*/*.parquet', hive_partitioning = true, hive_types_autocast = false);
