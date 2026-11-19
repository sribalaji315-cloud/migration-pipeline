-- API query: occurrences of one issue group, paged. $1 = group_key, $2 = issue_type, $3 = limit, $4 = offset
SELECT item_id, parent_item_id, legacy_feature_id, legacy_value, attribute_id, value_code, global_variable, value_list_id, detail,
       count(*) OVER () AS total
FROM review_issues
WHERE group_key = $1 AND issue_type = $2
ORDER BY item_id, attribute_id, value_code, legacy_feature_id, legacy_value
LIMIT $3 OFFSET $4;
