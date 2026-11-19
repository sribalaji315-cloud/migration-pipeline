-- Step 7 InheritanceStep (finish): resolve owners downward and emit line-level links with GSST00 provenance.
CREATE OR REPLACE TABLE inh_owner AS
WITH RECURSIVE o(item_id, attribute_id, owner_id) AS (
    SELECT e.child_id, e.attribute_id, e.parent_id
    FROM inh_edge e
    JOIN item_attr a ON a.item_id = e.parent_id AND a.attribute_id = e.attribute_id
    UNION
    SELECT e.child_id, e.attribute_id, o.owner_id
    FROM inh_edge e
    JOIN o ON o.item_id = e.parent_id AND o.attribute_id = e.attribute_id
)
SELECT DISTINCT item_id, attribute_id, owner_id FROM o ORDER BY item_id, attribute_id, owner_id;

CREATE OR REPLACE TABLE inheritance_link_s AS
WITH po AS (
    SELECT e.child_id, e.parent_id, e.attribute_id, e.level, e.parent_id AS owner_id
    FROM inh_edge e
    JOIN item_attr a ON a.item_id = e.parent_id AND a.attribute_id = e.attribute_id
    UNION
    SELECT e.child_id, e.parent_id, e.attribute_id, e.level, o.owner_id
    FROM inh_edge e
    JOIN inh_owner o ON o.item_id = e.parent_id AND o.attribute_id = e.attribute_id
)
SELECT
    l.bom_line_id, po.parent_id, po.child_id, l.bom_type, l.bom_position, l.child_model_no, l.is_pseudo_line,
    l.date_in, l.date_out, po.attribute_id, po.owner_id, po.level
FROM po
JOIN effective_bom_line l ON l.parent_item_id = po.parent_id AND l.child_item_id = po.child_id
ORDER BY po.child_id, po.attribute_id, po.parent_id, l.bom_line_id, po.owner_id;

CREATE OR REPLACE TABLE top_missing_agg AS
SELECT top_item_id, attribute_id, min(level) AS level FROM top_missing GROUP BY ALL ORDER BY ALL;
DROP TABLE top_missing;
ALTER TABLE top_missing_agg RENAME TO top_missing;

DROP TABLE inh_frontier;
