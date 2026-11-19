-- API query: nodes of one top BOM's explosion (cycle edges excluded). $1 = top_item_id, $2 = max nodes
SELECT w.item_id, c.description, coalesce(r.status, 'NOT_IMPORTED') AS status, t.resolved_type, coalesce(t.type_changed, false) AS type_changed,
       count(*) OVER () AS total
FROM bom_wave_items w
LEFT JOIN item_class c USING (item_id)
LEFT JOIN item_readiness r USING (item_id)
LEFT JOIN item_type_resolved t USING (item_id)
WHERE w.top_item_id = $1
ORDER BY w.item_id
LIMIT $2;
