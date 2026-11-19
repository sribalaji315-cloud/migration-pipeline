-- API query: used values not covered by the assigned global variable / value list (HIGHLIGHT). $1 = item_id, $2 = attribute_id
SELECT value_code, global_variable, value_list_id, value_type, is_list
FROM attr_missing_values
WHERE item_id = $1 AND attribute_id = $2
ORDER BY value_code;
