-- API query: class header + readiness of one item. $1 = item_id
SELECT c.item_id, c.class_id, c.parent_class_id, c.description, r.status AS readiness,
       t.resolved_type, t.type_changed
FROM item_class c
LEFT JOIN item_readiness r USING (item_id)
LEFT JOIN item_type_resolved t USING (item_id)
WHERE c.item_id = $1;
