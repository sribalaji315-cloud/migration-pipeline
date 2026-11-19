-- Step 5 ClassAttributeStep
-- Inputs : mt_scope_item, mt_item, swing_item, map_rules_clean, bs_variable
-- Outputs: item_class, item_used_value, item_attr
-- Rules  : R7 one class per migrated item (CL_<item>, parent CL_MIGRATED_ROOT, hidden); R8 attributes from the mapping API;
--          R5 PENDING_REVIEW rows are not used; R9 in_bs = attribute is a non-obsolete Bluestar VARIABLENAME.

CREATE OR REPLACE TABLE item_class AS
SELECT
    s.item_id,
    'CL_' || s.item_id AS class_id,
    'CL_MIGRATED_ROOT' AS parent_class_id,
    coalesce(m.description, d.description_1) AS description
FROM mt_scope_item s
LEFT JOIN mt_item m USING (item_id)
LEFT JOIN (SELECT item_id, min(description_1) AS description_1 FROM swing_item GROUP BY item_id) d USING (item_id)
ORDER BY s.item_id;

CREATE OR REPLACE TABLE item_used_value AS
SELECT DISTINCT c.item_id, c.target_attribute AS attribute_id, c.target_value AS value_code
FROM map_rules_clean c
WHERE c.load_status = 'READY'
ORDER BY item_id, attribute_id, value_code;

CREATE OR REPLACE TABLE item_attr AS
SELECT
    u.item_id,
    u.attribute_id,
    list(u.value_code ORDER BY u.value_code) AS used_values,
    count(*) AS n_used,
    u.attribute_id IN (SELECT variable_name FROM bs_variable) AS in_bs
FROM item_used_value u
GROUP BY u.item_id, u.attribute_id
ORDER BY u.item_id, u.attribute_id;
