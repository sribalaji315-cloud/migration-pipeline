-- Step 7 InheritanceStep (one level; repeated from C# until the frontier is empty).
INSERT INTO inh_edge
SELECT DISTINCT f.item_id, e.parent_item_id, f.attribute_id, f.level + 1
FROM inh_frontier f
JOIN trav_edge e ON e.child_item_id = f.item_id;

-- R15: the need reached a top (no parent) without an owner.
INSERT INTO top_missing
SELECT DISTINCT f.item_id, f.attribute_id, f.level
FROM inh_frontier f
WHERE NOT EXISTS (SELECT 1 FROM trav_edge e WHERE e.child_item_id = f.item_id);

CREATE OR REPLACE TABLE inh_frontier AS
SELECT DISTINCT e.parent_item_id AS item_id, f.attribute_id, f.level + 1 AS level
FROM inh_frontier f
JOIN trav_edge e ON e.child_item_id = f.item_id
WHERE NOT EXISTS (SELECT 1 FROM item_attr a WHERE a.item_id = e.parent_item_id AND a.attribute_id = f.attribute_id)
  AND NOT EXISTS (SELECT 1 FROM inh_node n WHERE n.item_id = e.parent_item_id AND n.attribute_id = f.attribute_id);

INSERT INTO inh_node SELECT item_id, attribute_id, level FROM inh_frontier;
