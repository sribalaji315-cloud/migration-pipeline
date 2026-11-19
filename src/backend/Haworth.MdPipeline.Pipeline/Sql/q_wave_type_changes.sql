-- API query: FIXED -> CONFIGURABLE items inside the wave, compared against SQLite decisions. $1 = top item ids joined with chr(31)
SELECT DISTINCT t.item_id, t.inherited_attrs, CAST(t.swing_type_code AS VARCHAR) AS swing_type_code
FROM bom_wave_items w
JOIN item_type_resolved t USING (item_id)
WHERE w.top_item_id IN (SELECT unnest(string_split($1, chr(31)))) AND t.type_changed
ORDER BY t.item_id;
