-- Step 12 ReadinessStep
-- Inputs : review_issues, item_type_resolved, bom_wave_items, cfg_type_decision, cfg_exported_item (from SQLite), cfg_rules
-- Outputs: item_readiness, bom_readiness
-- A type decision counts only if it still matches the current inherited attributes and Swing type code.

CREATE OR REPLACE TABLE item_readiness AS
WITH iss AS (
    SELECT item_id,
           count(*) FILTER (WHERE severity = 'Block')  AS block_issues,
           count(*) FILTER (WHERE severity = 'Review') AS review_issues
    FROM review_issues
    GROUP BY item_id
),
dec AS (
    SELECT t.item_id,
           t.type_changed AND (d.item_id IS NULL OR d.decision = 'Pending'
               OR d.inherited_attrs IS DISTINCT FROM t.inherited_attrs
               OR d.swing_type_code IS DISTINCT FROM CAST(t.swing_type_code AS VARCHAR)) AS pending,
           t.type_changed AND d.decision = 'Rejected'
               AND d.inherited_attrs IS NOT DISTINCT FROM t.inherited_attrs
               AND d.swing_type_code IS NOT DISTINCT FROM CAST(t.swing_type_code AS VARCHAR) AS rejected
    FROM item_type_resolved t
    LEFT JOIN cfg_type_decision d USING (item_id)
)
SELECT
    s.item_id,
    coalesce(i.block_issues, 0) AS block_issues,
    coalesce(i.review_issues, 0) AS review_issues,
    CAST(coalesce(d.pending, false) AS INTEGER) AS decisions,
    CASE
        WHEN coalesce(i.block_issues, 0) > 0 OR coalesce(d.rejected, false) THEN 'BLOCKED'
        WHEN coalesce(i.review_issues, 0) > 0 THEN CASE WHEN r.review_blocks THEN 'BLOCKED' ELSE 'REVIEW' END
        WHEN coalesce(d.pending, false) THEN 'DECISION'
        ELSE 'READY'
    END AS status
FROM mt_scope_item s
LEFT JOIN iss i USING (item_id)
LEFT JOIN dec d USING (item_id)
CROSS JOIN cfg_rules r
ORDER BY s.item_id;

CREATE OR REPLACE TABLE bom_readiness AS
SELECT
    w.top_item_id,
    count(*) AS items,
    count(*) FILTER (WHERE r.status = 'BLOCKED' OR r.item_id IS NULL) AS blocked_items,
    count(*) FILTER (WHERE r.status = 'REVIEW') AS review_items,
    count(*) FILTER (WHERE r.status = 'DECISION') AS decision_items,
    count(*) FILTER (WHERE w.item_id IN (SELECT item_id FROM cfg_exported_item)) AS already_exported_items,
    count(*) FILTER (WHERE r.item_id IS NULL) AS not_imported_items,
    CASE
        WHEN count(*) FILTER (WHERE r.status = 'BLOCKED' OR r.item_id IS NULL) > 0 THEN 'BLOCKED'
        WHEN count(*) FILTER (WHERE r.status = 'REVIEW') > 0 THEN 'REVIEW'
        WHEN count(*) FILTER (WHERE r.status = 'DECISION') > 0 THEN 'DECISION'
        WHEN count(*) = count(*) FILTER (WHERE w.item_id IN (SELECT item_id FROM cfg_exported_item)) THEN 'EXPORTED'
        ELSE 'READY'
    END AS status
FROM bom_wave_items w
LEFT JOIN item_readiness r USING (item_id)
GROUP BY w.top_item_id
ORDER BY w.top_item_id;
