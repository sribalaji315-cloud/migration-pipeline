-- API query: resolved type of one item (R16). $1 = item_id
SELECT source_type, resolved_type, type_changed, has_inherited, inherited_attrs
FROM item_type_resolved
WHERE item_id = $1;
