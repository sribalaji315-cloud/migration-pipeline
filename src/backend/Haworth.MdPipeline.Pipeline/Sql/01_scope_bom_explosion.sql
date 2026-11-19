-- Step 1 (BOM-scoped Migration Tool import): selected items + every descendant over all staged GSST00 lines.
-- Same definition as q_bom_scope.sql (all lines, trimmed ids; cycles terminate via UNION). $1 = ids joined with chr(31).
WITH RECURSIVE
lines AS (
    SELECT NULLIF(trim(STANR), '') AS parent_item_id, NULLIF(trim(STANRU), '') AS child_item_id
    FROM read_parquet('{{bronze_dir}}/swing_bom_raw.parquet')
),
tops AS (
    SELECT DISTINCT trim(x) AS item_id
    FROM unnest(string_split($1, chr(31))) AS t(x)
    WHERE trim(x) <> ''
),
scope(item_id) AS (
    SELECT item_id FROM tops
    UNION
    SELECT l.child_item_id
    FROM lines l
    JOIN scope s ON l.parent_item_id = s.item_id
    WHERE l.child_item_id IS NOT NULL
)
SELECT item_id FROM scope ORDER BY item_id;
