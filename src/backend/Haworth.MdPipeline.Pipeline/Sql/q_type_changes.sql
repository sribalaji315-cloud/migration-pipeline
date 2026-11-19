-- API query: FIXED -> CONFIGURABLE items (R16) with description and the number of top BOMs containing them.
SELECT t.item_id, c.description, CAST(t.swing_type_code AS VARCHAR) AS swing_type_code, t.source_type, t.resolved_type, t.inherited_attrs,
       (SELECT count(DISTINCT w.top_item_id) FROM bom_wave_items w WHERE w.item_id = t.item_id) AS affected_boms
FROM item_type_resolved t
LEFT JOIN item_class c USING (item_id)
WHERE t.type_changed
ORDER BY t.item_id;
