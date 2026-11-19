-- Step 4 MappingStep
-- Inputs : mt_mappings, mt_items (04a-c), cfg_rules
-- Outputs: map_src, map_rules_excluded, map_rules_clean, mt_item, mt_scope_item
-- Rules  : R1 engineering only; R2 NOT REQUIRED excluded; R3 blank target -> MAPPING_MISSING; R4 ignored/unreachable excluded
--          (missing legacy_reachable per cfg_rules); R5 predicted / feasibility Review -> PENDING_REVIEW; R6 trim, case-sensitive.

CREATE OR REPLACE TABLE map_src AS
SELECT
    row_number() OVER (ORDER BY item_id, legacy_feature, legacy_value, target_attribute, target_value, condition, mapped_from) AS mapping_row_id,
    *
FROM (
    SELECT
        NULLIF(trim(legacy_item_id), '')   AS item_id,
        NULLIF(trim(legacy_feature_id), '') AS legacy_feature,
        trim(legacy_value)                  AS legacy_value,
        NULLIF(trim(new_attribute_id), '')  AS target_attribute,
        NULLIF(trim(new_value), '')         AS target_value,
        lower(trim(attribute_type))         AS attribute_type,
        NULLIF(trim(condition), '')         AS condition,
        NULLIF(trim(simplified_legacy_condition), '') AS simplified_legacy_condition,
        NULLIF(trim(target_condition), '')  AS target_condition,
        lower(trim(mapped_from))            AS mapped_from,
        lower(trim(value_status))           AS value_status,
        trim(feasibility)                   AS feasibility,
        legacy_reachable
    FROM mt_mappings
);

CREATE OR REPLACE TABLE map_rules_excluded AS
SELECT * FROM (
    SELECT s.*,
        CASE
            WHEN s.item_id IS NULL THEN 'ITEM_MISSING'
            WHEN coalesce(s.attribute_type, '') <> 'engineering' THEN 'NOT_ENGINEERING'
            WHEN s.target_value = 'NOT REQUIRED' THEN 'NOT_REQUIRED'
            WHEN s.target_attribute IS NULL OR s.target_value IS NULL THEN 'MAPPING_MISSING'
            WHEN s.value_status = 'ignored' THEN 'IGNORED'
            WHEN s.legacy_reachable = false OR (s.legacy_reachable IS NULL AND NOT r.reachable_null_included) THEN 'UNREACHABLE'
        END AS reason
    FROM map_src s
    CROSS JOIN cfg_rules r
)
WHERE reason IS NOT NULL
ORDER BY mapping_row_id;

CREATE OR REPLACE TABLE map_rules_clean AS
SELECT
    s.mapping_row_id,
    s.item_id,
    s.legacy_feature,
    s.legacy_value,
    s.target_attribute,
    s.target_value,
    s.condition,
    s.simplified_legacy_condition,
    s.target_condition,
    s.mapped_from,
    s.feasibility,
    coalesce(s.value_status = 'discontinued', false) AS is_discontinued,
    CASE WHEN s.mapped_from = 'predicted' OR s.feasibility = 'Review' THEN 'PENDING_REVIEW' ELSE 'READY' END AS load_status
FROM map_src s
WHERE s.mapping_row_id NOT IN (SELECT mapping_row_id FROM map_rules_excluded)
ORDER BY s.mapping_row_id;

CREATE OR REPLACE TABLE mt_item AS
SELECT item_id, min(description) AS description, min(category) AS category, bool_and(approved_for_migration) AS approved_for_migration
FROM (SELECT NULLIF(trim(item_id), '') AS item_id, NULLIF(trim(description), '') AS description,
             NULLIF(trim(category), '') AS category, approved_for_migration FROM mt_items)
WHERE item_id IS NOT NULL
GROUP BY item_id
ORDER BY item_id;

-- Analysis scope: every item the Migration Tool was asked about (headers fetched) or has mappings for.
CREATE OR REPLACE TABLE mt_scope_item AS
SELECT DISTINCT item_id FROM (
    SELECT item_id FROM mt_item
    UNION ALL
    SELECT item_id FROM map_src WHERE item_id IS NOT NULL
)
ORDER BY item_id;
