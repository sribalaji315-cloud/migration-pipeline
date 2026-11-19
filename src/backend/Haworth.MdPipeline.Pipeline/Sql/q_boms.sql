-- API query: selectable BOMs. $1 = search text ('' = all), $2 = top-level only, $3 = limit, $4 = offset
SELECT item_id, description, line_count, child_count, is_top, count(*) OVER () AS total
FROM bom_parent
WHERE ($2 = false OR is_top)
  AND ($1 = '' OR item_id ILIKE '%' || $1 || '%' OR coalesce(description, '') ILIKE '%' || $1 || '%')
ORDER BY item_id
LIMIT $3 OFFSET $4;
