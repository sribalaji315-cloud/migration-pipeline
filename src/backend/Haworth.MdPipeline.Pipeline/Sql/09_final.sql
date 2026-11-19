-- Step 9 FinalModelStep
-- Inputs : item_class, item_attr, attr_gv_assigned, inh_node, inh_owner, map_rules_clean
-- Outputs: item_class_attribute, item_values_resolved, item_value_condition
-- Rules  : LOCAL attributes take their assigned GV/list (status GO/HIGHLIGHT, REVIEW when not a Bluestar variable);
--          INHERITED attributes take the owner's GV/list (R14). No owner, conflicting owner definitions or a non-Bluestar
--          attribute -> REVIEW (never silently pick an owner).

CREATE OR REPLACE TABLE item_class_attribute AS
WITH local AS (
    SELECT
        'CL_' || a.item_id AS class_id,
        a.item_id,
        a.attribute_id,
        'LOCAL' AS value_source,
        CASE WHEN a.in_bs THEN 'BLUESTAR' ELSE 'MAPPING_API' END AS attribute_source,
        g.global_variable,
        g.value_list_id,
        g.value_type,
        a.n_used,
        g.covered,
        CAST(NULL AS VARCHAR) AS inherited_from,
        CASE WHEN NOT a.in_bs THEN 'REVIEW' ELSE coalesce(g.status, 'REVIEW') END AS status,
        CASE WHEN NOT a.in_bs THEN 'ATTRIBUTE_NOT_IN_BS' END AS note
    FROM item_attr a
    LEFT JOIN attr_gv_assigned g USING (item_id, attribute_id)
),
inh AS (
    SELECT n.item_id, n.attribute_id, o.owner_id, g.global_variable, g.value_list_id, g.value_type
    FROM (SELECT DISTINCT item_id, attribute_id FROM inh_node) n
    LEFT JOIN inh_owner o USING (item_id, attribute_id)
    LEFT JOIN attr_gv_assigned g ON g.item_id = o.owner_id AND g.attribute_id = n.attribute_id
),
inherited AS (
    SELECT
        'CL_' || item_id AS class_id,
        item_id,
        attribute_id,
        'INHERITED' AS value_source,
        CASE WHEN attribute_id IN (SELECT variable_name FROM bs_variable) THEN 'BLUESTAR' ELSE 'MAPPING_API' END AS attribute_source,
        CASE WHEN n_defs = 1 THEN gv END AS global_variable,
        CASE WHEN n_defs = 1 THEN vl END AS value_list_id,
        CASE WHEN n_defs = 1 THEN vt END AS value_type,
        0 AS n_used,
        CAST(NULL AS BIGINT) AS covered,
        inherited_from,
        CASE
            WHEN n_owners = 0 THEN 'REVIEW'
            WHEN n_defs > 1 THEN 'REVIEW'
            WHEN attribute_id NOT IN (SELECT variable_name FROM bs_variable) THEN 'REVIEW'
            ELSE 'GO'
        END AS status,
        CASE
            WHEN n_owners = 0 THEN 'NO_OWNER'
            WHEN n_defs > 1 THEN 'OWNER_DEFINITION_CONFLICT'
            WHEN attribute_id NOT IN (SELECT variable_name FROM bs_variable) THEN 'ATTRIBUTE_NOT_IN_BS'
        END AS note
    FROM (
        SELECT
            item_id,
            attribute_id,
            array_to_string(list_sort(list_distinct(list(owner_id) FILTER (WHERE owner_id IS NOT NULL))), ',') AS inherited_from,
            count(DISTINCT owner_id) AS n_owners,
            count(DISTINCT coalesce(global_variable, '') || '|' || coalesce(value_list_id, '') || '|' || coalesce(value_type, ''))
                FILTER (WHERE owner_id IS NOT NULL) AS n_defs,
            min(global_variable) AS gv,
            min(value_list_id) AS vl,
            min(value_type) AS vt
        FROM inh
        GROUP BY item_id, attribute_id
    )
)
SELECT * FROM local
UNION ALL
SELECT * FROM inherited
ORDER BY item_id, attribute_id, value_source;

-- One row per item + attribute + value (READY mappings only); conditions are carried separately (not loaded).
CREATE OR REPLACE TABLE item_values_resolved AS
SELECT
    'AV-' || substr(sha256(c.item_id || chr(31) || c.target_attribute || chr(31) || c.target_value), 1, 20) AS value_id,
    c.item_id,
    c.target_attribute AS attribute_id,
    g.global_variable,
    g.value_list_id,
    c.target_value AS value_code,
    bool_and(c.is_discontinued) AS is_discontinued,
    count(*) FILTER (WHERE c.condition IS NOT NULL OR c.target_condition IS NOT NULL) AS n_conditions
FROM map_rules_clean c
LEFT JOIN attr_gv_assigned g ON g.item_id = c.item_id AND g.attribute_id = c.target_attribute
WHERE c.load_status = 'READY'
GROUP BY c.item_id, c.target_attribute, c.target_value, g.global_variable, g.value_list_id
ORDER BY c.item_id, attribute_id, value_code;

CREATE OR REPLACE TABLE item_value_condition AS
SELECT DISTINCT
    'AV-' || substr(sha256(c.item_id || chr(31) || c.target_attribute || chr(31) || c.target_value), 1, 20) AS value_id,
    c.condition AS legacy_condition,
    c.simplified_legacy_condition,
    c.target_condition
FROM map_rules_clean c
WHERE c.load_status = 'READY' AND (c.condition IS NOT NULL OR c.target_condition IS NOT NULL)
ORDER BY ALL;
