-- Step 11 IssuesStep
-- Inputs : mapping, class, GV, inheritance, type and Swing source tables (analysis scope only)
-- Outputs: review_issues (one row per occurrence), review_issue_groups (per group_key + issue_type)
-- States what is wrong, where and which source to fix. No suggestions.

CREATE OR REPLACE TABLE review_issues (
    issue_type VARCHAR, severity VARCHAR, group_key VARCHAR, item_id VARCHAR, parent_item_id VARCHAR,
    legacy_feature_id VARCHAR, legacy_value VARCHAR, attribute_id VARCHAR, value_code VARCHAR,
    global_variable VARCHAR, value_list_id VARCHAR, detail VARCHAR, fix_source VARCHAR, fix_target VARCHAR, fix_action VARCHAR
);

INSERT INTO review_issues
SELECT 'MAPPING_MISSING', 'Review', e.item_id || '|' || coalesce(e.legacy_feature, ''), e.item_id, NULL,
       e.legacy_feature, e.legacy_value, e.target_attribute, e.target_value, NULL, NULL,
       'Engineering mapping without target attribute or value', 'MtWorkspaceMapping', e.item_id,
       'Complete the target attribute and value in the Workspace mapping'
FROM map_rules_excluded e
WHERE e.reason = 'MAPPING_MISSING';

INSERT INTO review_issues
SELECT 'MAPPING_PENDING_REVIEW', 'Review', c.item_id || '|' || c.target_attribute, c.item_id, NULL,
       c.legacy_feature, c.legacy_value, c.target_attribute, c.target_value, NULL, NULL,
       'Mapping is ' || CASE WHEN c.mapped_from = 'predicted' THEN 'predicted' ELSE 'marked feasibility Review' END || ' and not used until confirmed',
       'MtWorkspaceMapping', c.item_id, 'Confirm or correct the mapping in the Workspace mapping'
FROM map_rules_clean c
WHERE c.load_status = 'PENDING_REVIEW';

INSERT INTO review_issues
SELECT 'ATTRIBUTE_NOT_IN_BS', 'Review', a.attribute_id, a.item_id, NULL, NULL, NULL, a.attribute_id, NULL, NULL, NULL,
       'Target attribute is not a (non-obsolete) Bluestar class variable', 'BluestarClassVariable', a.attribute_id,
       'Add the variable to a Bluestar class, or correct the target attribute in the Workspace mapping'
FROM item_attr a
WHERE NOT a.in_bs;

INSERT INTO review_issues
SELECT 'VALUES_NOT_IN_VALUELIST', 'Block',
       m.item_id || '|' || m.attribute_id || '|' || coalesce(m.global_variable, '') || '|' || m.value_list_id,
       m.item_id, NULL, NULL, NULL, m.attribute_id, m.value_code, m.global_variable, m.value_list_id,
       'Value not in value list ' || m.value_list_id, 'BluestarValueList', m.value_list_id,
       'Add missing values to ' || m.value_list_id || ', or correct target values in the Workspace mapping'
FROM attr_missing_values m
WHERE m.is_list;

INSERT INTO review_issues
SELECT 'VALUES_INVALID_FOR_TYPE', 'Block', m.item_id || '|' || m.attribute_id || '|' || coalesce(m.value_type, ''),
       m.item_id, NULL, NULL, NULL, m.attribute_id, m.value_code, m.global_variable, NULL,
       'Value is not a valid ' || coalesce(m.value_type, 'value'), 'MtWorkspaceMapping', m.item_id,
       'Correct the target value in the Workspace mapping'
FROM attr_missing_values m
WHERE NOT m.is_list;

INSERT INTO review_issues
SELECT 'INHERITED_ATTRIBUTE_MISSING_ON_TOP_ITEM', 'Block', t.top_item_id || '|' || t.attribute_id, t.top_item_id, NULL,
       NULL, NULL, t.attribute_id, NULL, NULL, NULL,
       'Attribute needed by a child (via conditions) reaches the BOM top without an owner (' || t.level || ' level(s) up)',
       'MtWorkspaceMapping', t.top_item_id, 'Map the attribute on the top item, or on a parent in between'
FROM top_missing t;

INSERT INTO review_issues
SELECT 'INHERITED_DEFINITION_CONFLICT', 'Review', a.item_id || '|' || a.attribute_id, a.item_id, NULL, NULL, NULL, a.attribute_id, NULL,
       NULL, NULL, 'Owners ' || a.inherited_from || ' use different global variables / value lists', 'MtWorkspaceMapping', a.item_id,
       'Align the owners'' target attributes in the Workspace mapping'
FROM item_class_attribute a
WHERE a.note = 'OWNER_DEFINITION_CONFLICT';

INSERT INTO review_issues
SELECT 'FIXED_TO_CONFIGURABLE', 'Approve', t.item_id, t.item_id, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       'Swing FIXED (ARVAKZ ' || coalesce(t.variant_flags_raw, '') || ') item inherits: ' || t.inherited_attrs,
       'MdPipelineDecision', t.item_id, 'Approve or reject the change to CONFIGURABLE'
FROM item_type_resolved t
WHERE t.type_changed;

INSERT INTO review_issues
SELECT 'ITEM_NOT_APPROVED_FOR_MIGRATION', 'Block', m.item_id, m.item_id, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       'Migration Tool approvedForMigration = false', 'MtMigrationApproval', m.item_id, 'Approve the item for migration in the Migration Tool'
FROM mt_item m
WHERE m.approved_for_migration = false;

INSERT INTO review_issues
SELECT 'MT_ITEM_HEADER_MISSING', 'Review', s.item_id, s.item_id, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       'Item has mappings but no Migration Tool item header (approval unknown)', 'MtMigrationApproval', s.item_id,
       'Check the item in the Migration Tool and re-import'
FROM mt_scope_item s
WHERE s.item_id NOT IN (SELECT item_id FROM mt_item);

-- Swing source issues on analysed items (item or parent in scope).
INSERT INTO review_issues
SELECT i.issue_type, i.severity,
       coalesce(i.parent_item_id, '') || '|' || coalesce(i.item_id, '') || '|' || coalesce(i.bom_line_id, ''),
       coalesce(i.item_id, i.parent_item_id), i.parent_item_id, NULL, NULL, NULL, NULL, NULL, NULL,
       i.detail, i.fix_source, coalesce(i.parent_item_id, i.item_id),
       CASE i.fix_source WHEN 'SwingItemMaster' THEN 'Correct the item in Swing GSAR00' ELSE 'Correct the BOM line in Swing GSST00' END
FROM swing_source_issue i
WHERE i.item_id IN (SELECT item_id FROM mt_scope_item) OR i.parent_item_id IN (SELECT item_id FROM mt_scope_item);

CREATE OR REPLACE TABLE review_issue_groups AS
WITH boms AS (
    SELECT r.group_key, r.issue_type, count(DISTINCT w.top_item_id) AS affected_boms
    FROM review_issues r
    JOIN bom_wave_items w ON w.item_id = r.item_id
    GROUP BY r.group_key, r.issue_type
)
SELECT
    r.group_key,
    r.issue_type,
    min(r.severity) AS severity,
    min(r.fix_source) AS fix_source,
    min(r.fix_target) AS fix_target,
    min(r.fix_action) AS fix_action,
    count(*) AS detail_rows,
    count(DISTINCT r.item_id) AS affected_items,
    coalesce(min(b.affected_boms), 0) AS affected_boms
FROM review_issues r
LEFT JOIN boms b USING (group_key, issue_type)
GROUP BY r.group_key, r.issue_type
ORDER BY r.issue_type, r.group_key;
