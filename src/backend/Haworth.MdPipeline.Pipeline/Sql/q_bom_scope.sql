-- API query: BOM scope = selected items + every descendant over all GSST00 lines (cycles terminate via UNION).
-- $1 = selected item ids joined with chr(31). Returns each scoped item and whether it is known to Swing.
WITH RECURSIVE
tops AS (
    SELECT DISTINCT trim(x) AS item_id
    FROM unnest(string_split($1, chr(31))) AS t(x)
    WHERE trim(x) <> ''
),
scope(item_id) AS (
    SELECT item_id FROM tops
    UNION
    SELECT l.child_item_id
    FROM bom_line l
    JOIN scope s ON l.parent_item_id = s.item_id
    WHERE l.child_item_id IS NOT NULL
)
SELECT
    s.item_id,
    s.item_id IN (SELECT item_id FROM tops) AS is_selected,
    (EXISTS (SELECT 1 FROM swing_item i WHERE i.item_id = s.item_id)
        OR EXISTS (SELECT 1 FROM bom_parent b WHERE b.item_id = s.item_id)) AS is_known
FROM scope s
ORDER BY s.item_id;
