-- Step 2c configuration tables. Rows are appended from validated options (SwingInterpretation, BomTypePolicy).
CREATE OR REPLACE TABLE cfg_run (
    run_id               VARCHAR,
    effective_date       DATE,
    date_out_inclusive   BOOLEAN,
    two_digit_year_pivot INTEGER,
    bom_policy_mode      VARCHAR,
    bom_policy_json      VARCHAR
);
CREATE OR REPLACE TABLE cfg_type_code   (code INTEGER, source_type VARCHAR);
CREATE OR REPLACE TABLE cfg_pseudo_flag (raw VARCHAR, is_pseudo BOOLEAN);
CREATE OR REPLACE TABLE cfg_open_date   (kind VARCHAR, y INTEGER, m INTEGER, d INTEGER);
CREATE OR REPLACE TABLE cfg_bom_type    (bom_type VARCHAR);
CREATE OR REPLACE TABLE cfg_rules       (list_rule VARCHAR, reachable_null_included BOOLEAN, review_blocks BOOLEAN);
