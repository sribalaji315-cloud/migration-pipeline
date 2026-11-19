-- API query: class model of one item. $1 = item_id
SELECT
    a.attribute_id, a.value_source, a.attribute_source, a.global_variable, a.value_list_id, s.n_values AS value_list_size,
    a.value_type, a.n_used, a.covered, a.status, a.inherited_from, a.note
FROM item_class_attribute a
LEFT JOIN bs_vl_size s USING (value_list_id)
WHERE a.item_id = $1
ORDER BY CASE a.status WHEN 'HIGHLIGHT' THEN 0 WHEN 'REVIEW' THEN 1 ELSE 2 END, a.attribute_id, a.value_source;
