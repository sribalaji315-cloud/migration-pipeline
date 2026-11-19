-- API query: Swing item type for one item (GSAR00.ARVAKZ). $1 = item_id
SELECT item_id, variant_flags_raw, type_code, source_type, is_ambiguous, model_count
FROM swing_item_type
WHERE item_id = $1;
