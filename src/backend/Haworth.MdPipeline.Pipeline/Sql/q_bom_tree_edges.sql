-- API query: parent -> child edges inside one top BOM's explosion. $1 = top_item_id
SELECT e.parent_item_id, e.child_item_id
FROM trav_edge e
WHERE e.parent_item_id IN (SELECT item_id FROM bom_wave_items WHERE top_item_id = $1)
ORDER BY e.parent_item_id, e.child_item_id;
