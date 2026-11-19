-- Step 3 ReferenceStep
-- Inputs : Bluestar reference Parquet ({{reference_dir}}), cfg_rules
-- Outputs: bs_class, bs_global_variable, bs_vl_value, bs_vl_size, bs_class_var, bs_gv_candidate, bs_variable
-- Verified against the live export: the class variable's VALUELISTID is the global variable id (= GV VARIABLETYPE);
-- the global variable supplies the real value list (VALUELISTID) and data type (VARIABLEDATATYPE).
-- Templates make older reference uploads (different headers) readable: missing columns become NULL.

CREATE OR REPLACE TABLE tpl_bs_class (CLASSNAME VARCHAR, EXTENSCLASSNAME VARCHAR, ENABLEDFORCLASSIFICATION VARCHAR, CLASSDESCRIPTION VARCHAR, SUBTYPE VARCHAR);
CREATE OR REPLACE TABLE tpl_bs_class_variable (CLASSNAME VARCHAR, VARIABLENAME VARCHAR, DISPLAYNAME VARCHAR, VALUELISTID VARCHAR, VARIABLEVALUES VARCHAR, VALUETYPE VARCHAR, MANDATORY VARCHAR, OBSOLETE VARCHAR, SEQUENCE VARCHAR, UNIT VARCHAR);
CREATE OR REPLACE TABLE tpl_bs_global_variable (VARIABLETYPE VARCHAR, VALUELISTID VARCHAR, VARIABLEDATATYPE VARCHAR, VARIABLEVALUES VARCHAR, UNIT VARCHAR);
CREATE OR REPLACE TABLE tpl_bs_valuelist_value (VALUELISTID VARCHAR, VALUE VARCHAR, DESCRIPTION VARCHAR, SORTING VARCHAR);

CREATE OR REPLACE VIEW raw_bs_class AS
SELECT * FROM tpl_bs_class UNION ALL BY NAME SELECT * FROM read_parquet('{{reference_dir}}/bs_class.parquet');
CREATE OR REPLACE VIEW raw_bs_class_variable AS
SELECT * FROM tpl_bs_class_variable UNION ALL BY NAME SELECT * FROM read_parquet('{{reference_dir}}/bs_class_variable.parquet');
CREATE OR REPLACE VIEW raw_bs_global_variable AS
SELECT * FROM tpl_bs_global_variable UNION ALL BY NAME SELECT * FROM read_parquet('{{reference_dir}}/bs_global_variable.parquet');
CREATE OR REPLACE VIEW raw_bs_valuelist_value AS
SELECT * FROM tpl_bs_valuelist_value UNION ALL BY NAME SELECT * FROM read_parquet('{{reference_dir}}/bs_valuelist_value.parquet');

CREATE OR REPLACE TABLE bs_class AS
SELECT DISTINCT
    NULLIF(trim(CLASSNAME), '')        AS class_name,
    NULLIF(trim(EXTENSCLASSNAME), '')  AS extends_class,
    upper(trim(coalesce(ENABLEDFORCLASSIFICATION, ''))) IN ('YES', 'Y', 'TRUE', '1') AS enabled,
    NULLIF(trim(CLASSDESCRIPTION), '') AS description,
    NULLIF(trim(SUBTYPE), '')          AS subtype
FROM raw_bs_class
WHERE NULLIF(trim(CLASSNAME), '') IS NOT NULL
ORDER BY class_name;

-- Value list values are space-padded in the export: compare on TRIM, case-sensitive (R6).
CREATE OR REPLACE TABLE bs_vl_value AS
SELECT value_list_id, value_code, min(description) AS description, min(sorting) AS sorting
FROM (
    SELECT NULLIF(trim(VALUELISTID), '') AS value_list_id,
           NULLIF(trim(VALUE), '')       AS value_code,
           NULLIF(trim(DESCRIPTION), '') AS description,
           TRY_CAST(trim(SORTING) AS INTEGER) AS sorting
    FROM raw_bs_valuelist_value
)
WHERE value_list_id IS NOT NULL AND value_code IS NOT NULL
GROUP BY value_list_id, value_code
ORDER BY value_list_id, value_code;

CREATE OR REPLACE TABLE bs_vl_size AS
SELECT value_list_id, count(*) AS n_values FROM bs_vl_value GROUP BY value_list_id ORDER BY value_list_id;

CREATE OR REPLACE TABLE bs_global_variable AS
SELECT global_variable, min(value_list_id) AS value_list_id, min(value_type) AS value_type, bool_or(gv_is_list) AS gv_is_list, min(unit) AS unit
FROM (
    SELECT NULLIF(trim(VARIABLETYPE), '')     AS global_variable,
           NULLIF(trim(VALUELISTID), '')      AS value_list_id,
           NULLIF(trim(VARIABLEDATATYPE), '') AS value_type,
           upper(trim(coalesce(VARIABLEVALUES, ''))) = 'VALUE_LIST' AS gv_is_list,
           NULLIF(trim(UNIT), '')             AS unit
    FROM raw_bs_global_variable
)
WHERE global_variable IS NOT NULL
GROUP BY global_variable
ORDER BY global_variable;

CREATE OR REPLACE TABLE bs_class_var AS
WITH cv AS (
    SELECT
        NULLIF(trim(CLASSNAME), '')    AS class_name,
        NULLIF(trim(VARIABLENAME), '') AS variable_name,
        NULLIF(trim(DISPLAYNAME), '')  AS display_name,
        NULLIF(trim(VALUELISTID), '')  AS gv_ref,
        NULLIF(trim(VALUETYPE), '')    AS value_type,
        upper(trim(coalesce(VARIABLEVALUES, ''))) = 'VALUE_LIST' AS cv_is_list,
        upper(trim(coalesce(MANDATORY, ''))) IN ('YES', 'Y', 'TRUE', '1') AS mandatory,
        upper(trim(coalesce(OBSOLETE, ''))) IN ('YES', 'Y', 'TRUE', '1')  AS obsolete,
        TRY_CAST(trim(SEQUENCE) AS INTEGER) AS sequence,
        NULLIF(trim(UNIT), '') AS unit
    FROM raw_bs_class_variable
)
SELECT
    cv.class_name,
    cv.variable_name,
    cv.display_name,
    gv.global_variable,
    -- Without a global variable, a VALUELISTID naming an existing value list is still that list.
    coalesce(gv.value_list_id, CASE WHEN gv.global_variable IS NULL AND cv.gv_ref IN (SELECT value_list_id FROM bs_vl_size) THEN cv.gv_ref END) AS value_list_id,
    coalesce(cv.value_type, gv.value_type) AS value_type,
    cv.cv_is_list,
    coalesce(gv.gv_is_list, false) AS gv_is_list,
    cv.mandatory,
    cv.obsolete,
    cv.sequence,
    coalesce(cv.unit, gv.unit) AS unit
FROM cv
LEFT JOIN bs_global_variable gv ON gv.global_variable = cv.gv_ref
WHERE cv.class_name IS NOT NULL AND cv.variable_name IS NOT NULL
ORDER BY cv.class_name, cv.variable_name;

-- R10: candidates = distinct (global variable, value list, value type) per variable name over non-obsolete class variables.
CREATE OR REPLACE TABLE bs_gv_candidate AS
WITH c AS (
    SELECT v.*,
           (CASE r.list_rule
                WHEN 'ClassVariable'  THEN v.cv_is_list
                WHEN 'GlobalVariable' THEN v.gv_is_list
                ELSE v.cv_is_list OR v.gv_is_list
            END) AND v.value_list_id IN (SELECT value_list_id FROM bs_vl_size) AS is_list
    FROM bs_class_var v
    CROSS JOIN cfg_rules r
    WHERE NOT v.obsolete
)
SELECT
    variable_name,
    global_variable,
    CASE WHEN is_list THEN value_list_id END AS value_list_id,
    value_type,
    is_list,
    min(unit) AS unit,
    count(DISTINCT class_name) AS class_usage
FROM c
GROUP BY variable_name, global_variable, CASE WHEN is_list THEN value_list_id END, value_type, is_list
ORDER BY variable_name, global_variable NULLS LAST, value_list_id NULLS LAST, value_type;

-- R9: valid target attributes = non-obsolete Bluestar VARIABLENAMEs.
CREATE OR REPLACE TABLE bs_variable AS
SELECT DISTINCT variable_name FROM bs_class_var WHERE NOT obsolete ORDER BY variable_name;
