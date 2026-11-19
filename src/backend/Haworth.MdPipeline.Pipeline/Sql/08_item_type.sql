-- Step 8 ItemTypeStep
-- Inputs : mt_scope_item, swing_item_type (GSAR00.ARVAKZ), inh_node
-- Output : item_type_resolved
-- Rule   : R16 a Swing FIXED item with any INHERITED attribute becomes CONFIGURABLE (Approver decision required).
--          Pseudo is per BOM line and never changes the item type. UNKNOWN stays UNKNOWN.

CREATE OR REPLACE TABLE item_type_resolved AS
WITH inh AS (
    SELECT item_id, array_to_string(list_sort(list_distinct(list(attribute_id))), ',') AS inherited_attrs
    FROM inh_node
    GROUP BY item_id
)
SELECT
    s.item_id,
    t.type_code AS swing_type_code,
    t.variant_flags_raw,
    coalesce(t.source_type, 'UNKNOWN') AS source_type,
    inh.item_id IS NOT NULL AS has_inherited,
    coalesce(inh.inherited_attrs, '') AS inherited_attrs,
    CASE WHEN t.source_type = 'FIXED' AND inh.item_id IS NOT NULL THEN 'CONFIGURABLE' ELSE coalesce(t.source_type, 'UNKNOWN') END AS resolved_type,
    coalesce(t.source_type = 'FIXED' AND inh.item_id IS NOT NULL, false) AS type_changed,
    (SELECT max(r._extracted_at) FROM swing_item_raw r) AS type_extracted_at
FROM mt_scope_item s
LEFT JOIN swing_item_type t USING (item_id)
LEFT JOIN inh USING (item_id)
ORDER BY s.item_id;
