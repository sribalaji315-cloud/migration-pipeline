-- API query: wave gate counts over the union of the wave's top BOM explosions. $1 = top item ids joined with chr(31)
WITH items AS (
    SELECT DISTINCT item_id FROM bom_wave_items WHERE top_item_id IN (SELECT unnest(string_split($1, chr(31))))
)
SELECT
    (SELECT count(*) FROM items) AS items,
    (SELECT count(*) FROM (SELECT unnest(string_split($1, chr(31))) AS x) WHERE x NOT IN (SELECT top_item_id FROM top_bom)) AS unknown_tops,
    (SELECT count(*) FROM items i JOIN item_readiness r USING (item_id) CROSS JOIN cfg_rules c
      WHERE r.block_issues > 0 OR (c.review_blocks AND r.review_issues > 0)) AS issue_items,
    (SELECT count(*) FROM items WHERE item_id NOT IN (SELECT item_id FROM mt_scope_item)) AS not_imported,
    (SELECT count(*) FROM items i JOIN item_type_resolved t USING (item_id) WHERE t.resolved_type = 'UNKNOWN') AS unknown_types,
    (SELECT count(*) FROM item_class_attribute a JOIN items USING (item_id)
      WHERE (a.global_variable IS NOT NULL AND a.global_variable NOT IN (SELECT global_variable FROM bs_global_variable))
         OR (a.value_list_id IS NOT NULL AND a.value_list_id NOT IN (SELECT value_list_id FROM bs_vl_size))) AS missing_references;
