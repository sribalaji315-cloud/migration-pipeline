-- API query: distinct items of the union of several top BOM explosions. $1 = top item ids joined with chr(31)
SELECT DISTINCT w.item_id
FROM bom_wave_items w
WHERE w.top_item_id IN (SELECT unnest(string_split($1, chr(31))))
ORDER BY w.item_id;
