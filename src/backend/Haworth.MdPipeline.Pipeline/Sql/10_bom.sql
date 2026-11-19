-- Step 10 BomStep
-- Inputs : mt_scope_item, trav_edge
-- Outputs: top_bom, bom_wave_items
-- top_bom = analysed items without a parent inside the analysis scope (imported top BOMs or imported sub-assemblies).
-- bom_wave_items = each top's effective explosion (cycle edges excluded; UNION keeps it set-based and finite).

CREATE OR REPLACE TABLE top_bom AS
SELECT s.item_id AS top_item_id
FROM mt_scope_item s
WHERE NOT EXISTS (
    SELECT 1 FROM trav_edge e JOIN mt_scope_item p ON p.item_id = e.parent_item_id
    WHERE e.child_item_id = s.item_id
)
ORDER BY top_item_id;

CREATE OR REPLACE TABLE bom_wave_items AS
WITH RECURSIVE x(top_item_id, item_id) AS (
    SELECT top_item_id, top_item_id FROM top_bom
    UNION
    SELECT x.top_item_id, e.child_item_id FROM x JOIN trav_edge e ON e.parent_item_id = x.item_id
)
SELECT top_item_id, item_id FROM x ORDER BY top_item_id, item_id;
