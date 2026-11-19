-- Step 4 (part 3): Bronze item headers view (only when items.parquet exists).
CREATE OR REPLACE VIEW mt_items AS
SELECT * FROM tpl_mt_item
UNION ALL BY NAME
SELECT * FROM read_parquet('{{bronze_dir}}/items.parquet');
