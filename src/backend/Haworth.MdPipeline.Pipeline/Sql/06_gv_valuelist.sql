-- Step 6 GlobalVariableStep
-- Inputs : item_attr, item_used_value, bs_gv_candidate, bs_vl_value
-- Outputs: attr_gv_check, attr_gv_assigned, attr_missing_values
-- Rules  : R11 coverage (list: used values in the list; free: used values valid for the value type);
--          R12 full-coverage list -> full-coverage free -> highest coverage; ties: class_usage, then name;
--          R13 GO = full coverage, else HIGHLIGHT with missing values. NA is an ordinary value (R6).

CREATE OR REPLACE TABLE attr_value_check AS
SELECT
    u.item_id,
    u.attribute_id,
    u.value_code,
    c.global_variable,
    c.value_list_id,
    c.value_type,
    c.is_list,
    c.class_usage,
    CASE
        WHEN c.is_list THEN v.value_code IS NOT NULL
        WHEN c.value_type = 'IntegerType' THEN TRY_CAST(u.value_code AS BIGINT) IS NOT NULL
        WHEN c.value_type = 'RealType' THEN TRY_CAST(u.value_code AS DOUBLE) IS NOT NULL
        ELSE true
    END AS is_valid
FROM item_used_value u
JOIN item_attr a ON a.item_id = u.item_id AND a.attribute_id = u.attribute_id AND a.in_bs
JOIN bs_gv_candidate c ON c.variable_name = u.attribute_id
LEFT JOIN bs_vl_value v ON c.is_list AND v.value_list_id = c.value_list_id AND v.value_code = u.value_code;

CREATE OR REPLACE TABLE attr_gv_check AS
SELECT
    item_id, attribute_id, global_variable, value_list_id, value_type, is_list, class_usage,
    count(*) AS n_used,
    count(*) FILTER (WHERE is_valid) AS covered
FROM attr_value_check
GROUP BY item_id, attribute_id, global_variable, value_list_id, value_type, is_list, class_usage
ORDER BY item_id, attribute_id, global_variable NULLS LAST, value_list_id NULLS LAST, value_type;

CREATE OR REPLACE TABLE attr_gv_assigned AS
WITH ranked AS (
    SELECT
        *,
        count(*) FILTER (WHERE is_list AND covered = n_used) OVER (PARTITION BY item_id, attribute_id) AS n_full_lists,
        row_number() OVER (
            PARTITION BY item_id, attribute_id
            ORDER BY
                CASE WHEN is_list AND covered = n_used THEN 0 WHEN NOT is_list AND covered = n_used THEN 1 ELSE 2 END,
                covered DESC,
                class_usage DESC,
                global_variable NULLS LAST,
                value_list_id NULLS LAST,
                value_type
        ) AS rn
    FROM attr_gv_check
)
SELECT
    item_id, attribute_id, global_variable, value_list_id, value_type, is_list, class_usage, n_used, covered, n_full_lists,
    CASE WHEN covered = n_used THEN 'GO' ELSE 'HIGHLIGHT' END AS status
FROM ranked
WHERE rn = 1
ORDER BY item_id, attribute_id;

CREATE OR REPLACE TABLE attr_missing_values AS
SELECT DISTINCT a.item_id, a.attribute_id, a.global_variable, a.value_list_id, a.value_type, a.is_list, v.value_code
FROM attr_gv_assigned a
JOIN attr_value_check v
  ON v.item_id = a.item_id AND v.attribute_id = a.attribute_id
 AND v.global_variable IS NOT DISTINCT FROM a.global_variable
 AND v.value_list_id IS NOT DISTINCT FROM a.value_list_id
 AND v.value_type IS NOT DISTINCT FROM a.value_type
 AND v.is_list = a.is_list
WHERE a.status = 'HIGHLIGHT' AND NOT v.is_valid
ORDER BY a.item_id, a.attribute_id, v.value_code;
