-- API query: group keys touching the explosion of one top BOM (issue filter "bom"). $1 = top_item_id
SELECT DISTINCT r.group_key, r.issue_type
FROM review_issues r
JOIN bom_wave_items w ON w.item_id = r.item_id
WHERE w.top_item_id = $1;
