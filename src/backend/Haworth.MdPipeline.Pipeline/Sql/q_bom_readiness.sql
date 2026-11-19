-- API query: BOM readiness, paged. $1 = search text ('' = all), $2 = status ('' = all), $3 = limit, $4 = offset
SELECT b.top_item_id, c.description, b.items, b.blocked_items, b.review_items, b.decision_items, b.already_exported_items,
       b.not_imported_items, b.status, count(*) OVER () AS total
FROM bom_readiness b
LEFT JOIN item_class c ON c.item_id = b.top_item_id
WHERE ($1 = '' OR b.top_item_id ILIKE '%' || $1 || '%' OR c.description ILIKE '%' || $1 || '%')
  AND ($2 = '' OR b.status = $2)
ORDER BY b.top_item_id
LIMIT $3 OFFSET $4;
