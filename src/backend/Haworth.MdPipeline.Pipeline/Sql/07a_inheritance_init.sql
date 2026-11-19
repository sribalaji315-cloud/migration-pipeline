-- Step 7 InheritanceStep (init)
-- Needs: OWN = attributes an item has values for; CONDITION = attributes referenced by its mapping conditions:
--   legacy features '#FEATURE' in the raw condition, mapped to target attributes via the clean mappings, plus attribute
--   names appearing in target_condition. A CONDITION need the item does not own is pushed to every direct parent (R14).
-- Traversal uses effective edges without cycle edges.

CREATE OR REPLACE TABLE trav_edge AS
SELECT e.parent_item_id, e.child_item_id
FROM bom_edge e
WHERE NOT EXISTS (SELECT 1 FROM bom_cycle_edge c WHERE c.parent_item_id = e.parent_item_id AND c.child_item_id = e.child_item_id)
ORDER BY e.parent_item_id, e.child_item_id;

CREATE OR REPLACE TABLE feature_attribute AS
SELECT DISTINCT legacy_feature, target_attribute AS attribute_id
FROM map_rules_clean
WHERE legacy_feature IS NOT NULL AND target_attribute IS NOT NULL
ORDER BY legacy_feature, attribute_id;

CREATE OR REPLACE TABLE item_need AS
SELECT DISTINCT item_id, attribute_id, reason FROM (
    SELECT item_id, attribute_id, 'OWN' AS reason FROM item_attr
    UNION ALL
    SELECT c.item_id, f.attribute_id, 'CONDITION'
    FROM (SELECT item_id, unnest(regexp_extract_all(condition, '#([A-Za-z0-9_]+)', 1)) AS legacy_feature
          FROM map_rules_clean WHERE condition IS NOT NULL AND load_status = 'READY') c
    JOIN feature_attribute f USING (legacy_feature)
    UNION ALL
    SELECT c.item_id, c.token, 'CONDITION'
    FROM (SELECT item_id, unnest(regexp_extract_all(target_condition, '[A-Za-z][A-Za-z0-9_]*')) AS token
          FROM map_rules_clean WHERE target_condition IS NOT NULL AND load_status = 'READY') c
    WHERE c.token IN (SELECT variable_name FROM bs_variable)
)
ORDER BY item_id, attribute_id, reason;

CREATE OR REPLACE TABLE inh_frontier AS
SELECT DISTINCT n.item_id, n.attribute_id, 0 AS level
FROM item_need n
WHERE n.reason = 'CONDITION'
  AND NOT EXISTS (SELECT 1 FROM item_attr a WHERE a.item_id = n.item_id AND a.attribute_id = n.attribute_id);

-- Items that hold an attribute by inheritance (the needing item and every non-owning item on the way up).
CREATE OR REPLACE TABLE inh_node AS SELECT item_id, attribute_id, level FROM inh_frontier;
CREATE OR REPLACE TABLE inh_edge (child_id VARCHAR, parent_id VARCHAR, attribute_id VARCHAR, level INTEGER);
CREATE OR REPLACE TABLE top_missing (top_item_id VARCHAR, attribute_id VARCHAR, level INTEGER);
