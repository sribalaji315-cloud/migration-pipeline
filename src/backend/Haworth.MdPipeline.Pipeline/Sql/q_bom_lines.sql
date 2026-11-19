-- API query: GSST00 lines of one parent with full provenance. $1 = parent_item_id
SELECT
    bom_line_id, parent_item_id, child_item_id, bom_type, bom_position, child_model_no,
    regexp_replace(CAST(quantity AS VARCHAR), '\.?0+$', '') AS quantity, quantity_raw, unit, is_pseudo_line, pseudo_raw,
    date_in, date_out, is_effective, included_by_policy, bom_condition_id
FROM bom_line
WHERE parent_item_id = $1
ORDER BY bom_position, bom_type, child_item_id, bom_line_id;
