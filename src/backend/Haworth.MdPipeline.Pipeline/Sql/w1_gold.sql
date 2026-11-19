-- Step W1 WaveGoldStep
-- Runs in the wave's Gold DuckDB with the analysis Silver attached read-only as "s".
-- Inputs : cfg_wave_top, cfg_wave_item (appended by C#), Silver item_class, item_class_attribute, item_type_resolved,
--          item_values_resolved, item_value_condition, inheritance_link_s, bs_global_variable, bs_class_var
-- Outputs: g01_class .. g06_eoav_condition (one table per export sheet, columns as in §8.3)
-- Gold never contains value list, value or global variable definitions; only references to them.

ATTACH '{{silver_path}}' AS s (READ_ONLY);

CREATE OR REPLACE TABLE g01_class AS
SELECT 'CL_MIGRATED_ROOT' AS ClassId, 'MIGRATED_ROOT' AS Name, 'Migrated items' AS Description, CAST(NULL AS VARCHAR) AS ParentClassId,
       true AS IsHidden, false AS EnabledForClassification, 0 AS sort_key
UNION ALL
SELECT c.class_id, c.item_id, c.description, c.parent_class_id, true, true, 1
FROM s.item_class c
JOIN cfg_wave_item w USING (item_id)
ORDER BY sort_key, ClassId;
ALTER TABLE g01_class DROP COLUMN sort_key;

CREATE OR REPLACE TABLE g02_class_attribute AS
SELECT
    a.class_id || '|' || a.attribute_id AS ClassAttributeId,
    a.class_id AS ClassId,
    a.attribute_id AS VariableName,
    a.global_variable AS GlobalVariable,
    a.value_list_id AS ValueListId,
    a.value_type AS ValueType,
    g.unit AS Unit,
    a.value_source AS ValueSource,
    coalesce(m.mandatory, false) AS IsMandatory,
    row_number() OVER (PARTITION BY a.class_id ORDER BY a.value_source DESC, a.attribute_id) AS Sequence
FROM s.item_class_attribute a
JOIN cfg_wave_item w USING (item_id)
LEFT JOIN s.bs_global_variable g ON g.global_variable = a.global_variable
LEFT JOIN (SELECT variable_name, bool_and(mandatory) AS mandatory FROM s.bs_class_var WHERE NOT obsolete GROUP BY variable_name) m
       ON m.variable_name = a.attribute_id
ORDER BY ClassId, Sequence;

CREATE OR REPLACE TABLE g03_engineering_object AS
SELECT
    c.item_id AS ObjectId,
    c.class_id AS ClassId,
    c.description AS Description,
    CAST(t.swing_type_code AS VARCHAR) AS SwingTypeCode,
    t.source_type AS SourceType,
    t.resolved_type AS ProductType,
    t.type_changed AS TypeChanged,
    (SELECT array_to_string(list_sort(list(DISTINCT b.top_item_id)), ',')
       FROM s.bom_wave_items b JOIN cfg_wave_top x ON x.top_item_id = b.top_item_id
      WHERE b.item_id = c.item_id) AS TopBom
FROM s.item_class c
JOIN cfg_wave_item w USING (item_id)
LEFT JOIN s.item_type_resolved t USING (item_id)
ORDER BY ObjectId;

CREATE OR REPLACE TABLE g04_engineering_object_attribute_value AS
SELECT v.value_id AS ObjectAttributeValueId, v.item_id AS ObjectId, v.attribute_id AS VariableName, v.value_list_id AS ValueListId,
       v.value_code AS ValueCode, NOT v.is_discontinued AS IsActive
FROM s.item_values_resolved v
JOIN cfg_wave_item w USING (item_id)
ORDER BY ObjectId, VariableName, ValueCode;

CREATE OR REPLACE TABLE g05_inheritance_link AS
SELECT DISTINCT
    'CL_' || l.parent_id AS ParentClassId,
    'CL_' || l.child_id AS ChildClassId,
    l.attribute_id AS VariableName,
    'CL_' || l.owner_id AS OwnerClassId,
    l.bom_line_id AS BomLineId,
    l.bom_type AS BomType,
    l.bom_position AS BomPosition,
    l.child_model_no AS ChildModelNo,
    l.is_pseudo_line AS IsPseudoLine,
    l.date_in AS DateIn,
    l.date_out AS DateOut
FROM s.inheritance_link_s l
WHERE l.child_id IN (SELECT item_id FROM cfg_wave_item) AND l.parent_id IN (SELECT item_id FROM cfg_wave_item)
ORDER BY ChildClassId, VariableName, ParentClassId, BomLineId;

CREATE OR REPLACE TABLE g06_eoav_condition AS
SELECT c.value_id AS ObjectAttributeValueId, c.legacy_condition AS LegacyCondition,
       c.simplified_legacy_condition AS SimplifiedLegacyCondition, c.target_condition AS TargetCondition
FROM s.item_value_condition c
WHERE c.value_id IN (SELECT ObjectAttributeValueId FROM g04_engineering_object_attribute_value)
ORDER BY ALL;

CREATE OR REPLACE TABLE g_issue_groups AS
SELECT DISTINCT g.*
FROM s.review_issue_groups g
JOIN s.review_issues r USING (group_key, issue_type)
JOIN cfg_wave_item w ON w.item_id = r.item_id
ORDER BY g.issue_type, g.group_key;

CREATE OR REPLACE TABLE g_issues AS
SELECT r.*
FROM s.review_issues r
JOIN cfg_wave_item w ON w.item_id = r.item_id
ORDER BY r.fix_source, r.issue_type, r.item_id, r.attribute_id, r.value_code;

DETACH s;
