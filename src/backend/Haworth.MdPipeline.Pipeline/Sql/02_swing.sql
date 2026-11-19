-- Step 2c NormalizeSwingStep
-- Inputs : Bronze swing_item_raw (GSAR00), swing_bom_raw (GSST00); cfg_* tables (02_swing_cfg.sql)
-- Outputs: swing_item, swing_item_type, bom_line, effective_bom_line, bom_edge, bom_edge_line,
--          bom_line_key_profile, swing_source_issue
-- Rules  : GSST00 is authoritative (R17). Identifiers are trimmed only (leading zeros and case preserved).
--          Unknown type codes, pseudo flags and date patterns are never defaulted; they raise issues.

CREATE OR REPLACE VIEW swing_item_raw AS SELECT * FROM read_parquet('{{bronze_dir}}/swing_item_raw.parquet');
CREATE OR REPLACE VIEW swing_bom_raw  AS SELECT * FROM read_parquet('{{bronze_dir}}/swing_bom_raw.parquet');

-- ---------------------------------------------------------------------------------------------
-- GSAR00 item master: one row per distinct validated (item, model) content.
-- ---------------------------------------------------------------------------------------------
CREATE OR REPLACE TABLE swing_item AS
SELECT DISTINCT
    NULLIF(trim(r.ARANR), '')                           AS item_id,
    NULLIF(trim(r.ARMONR), '')                          AS model_no,
    NULLIF(trim(r.ARABZ1), '')                          AS description_1,
    NULLIF(trim(r.ARABZ2), '')                          AS description_2,
    NULLIF(trim(r.ARPROB), '')                          AS product_range,
    NULLIF(trim(r.ARPROG), '')                          AS product_group,
    NULLIF(trim(r.ARARGR), '')                          AS item_group,
    NULLIF(trim(r.ARMEIN), '')                          AS unit,
    NULLIF(trim(r.ARMART), '')                          AS material_type,
    NULLIF(trim(r.ARVAKZ), '')                          AS variant_flag_raw,
    TRY_CAST(NULLIF(trim(r.ARVAKZ), '') AS INTEGER)     AS type_code,
    COALESCE(tc.source_type, 'UNKNOWN')                 AS source_type,
    NULLIF(trim(r.ARBDID), '')                          AS item_condition_id
FROM swing_item_raw r
LEFT JOIN cfg_type_code tc
       ON tc.code = TRY_CAST(NULLIF(trim(r.ARVAKZ), '') AS INTEGER)
WHERE NULLIF(trim(r.ARANR), '') IS NOT NULL
ORDER BY item_id, model_no;

-- One row per item. The type comes only from the base record (ARMONR blank); model rows (ARMONR filled) carry
-- model-specific flags and are ignored for the item type. Several base rows with different ARVAKZ are ambiguous:
-- no row is chosen and the type stays UNKNOWN. An item without a base record is UNKNOWN (ITEM_TYPE_UNKNOWN).
CREATE OR REPLACE TABLE swing_item_type AS
WITH agg AS (
    SELECT
        item_id,
        count(*) FILTER (WHERE model_no IS NULL)                                        AS source_rows,
        count(DISTINCT model_no)                                                        AS model_count,
        array_to_string(list_sort(list_distinct(list(coalesce(variant_flag_raw, '<blank>')) FILTER (WHERE model_no IS NULL))), ',') AS variant_flags_raw,
        count(DISTINCT coalesce(variant_flag_raw, '<blank>')) FILTER (WHERE model_no IS NULL) AS n_flags,
        min(type_code) FILTER (WHERE model_no IS NULL)                                  AS type_code,
        min(source_type) FILTER (WHERE model_no IS NULL)                                AS source_type
    FROM swing_item
    GROUP BY item_id
)
SELECT
    item_id,
    source_rows,
    model_count,
    variant_flags_raw,
    n_flags > 1                                               AS is_ambiguous,
    CASE WHEN n_flags = 1 THEN type_code END                  AS type_code,
    CASE WHEN n_flags = 1 THEN source_type ELSE 'UNKNOWN' END AS source_type
FROM agg
ORDER BY item_id;

-- ---------------------------------------------------------------------------------------------
-- GSST00 BOM lines: every source occurrence is kept (repeated positions/types included).
-- ---------------------------------------------------------------------------------------------
CREATE OR REPLACE TABLE bom_line AS
WITH src AS (
    SELECT r.*, row_number() OVER (PARTITION BY r._row_hash ORDER BY r._row_hash) AS dup_seq
    FROM swing_bom_raw r
),
parsed AS (
    SELECT
        s.*,
        TRY_CAST(NULLIF(trim(s.STABJJ), '') AS INTEGER) AS in_y,
        TRY_CAST(NULLIF(trim(s.STABMM), '') AS INTEGER) AS in_m,
        TRY_CAST(NULLIF(trim(s.STABTT), '') AS INTEGER) AS in_d,
        TRY_CAST(NULLIF(trim(s.STBIJJ), '') AS INTEGER) AS out_y,
        TRY_CAST(NULLIF(trim(s.STBIMM), '') AS INTEGER) AS out_m,
        TRY_CAST(NULLIF(trim(s.STBITT), '') AS INTEGER) AS out_d
    FROM src s
),
years AS (
    SELECT
        p.*,
        c.effective_date,
        c.date_out_inclusive,
        c.bom_policy_mode,
        EXISTS (SELECT 1 FROM cfg_open_date o WHERE o.kind = 'IN'  AND o.y = p.in_y  AND o.m = p.in_m  AND o.d = p.in_d)  AS in_open,
        EXISTS (SELECT 1 FROM cfg_open_date o WHERE o.kind = 'OUT' AND o.y = p.out_y AND o.m = p.out_m AND o.d = p.out_d) AS out_open,
        CASE
            WHEN p.in_y >= 1000 THEN p.in_y
            WHEN p.in_y BETWEEN 0 AND 99 AND c.two_digit_year_pivot IS NOT NULL
                THEN p.in_y + CASE WHEN p.in_y <= c.two_digit_year_pivot THEN 2000 ELSE 1900 END
        END AS in_year,
        CASE
            WHEN p.out_y >= 1000 THEN p.out_y
            WHEN p.out_y BETWEEN 0 AND 99 AND c.two_digit_year_pivot IS NOT NULL
                THEN p.out_y + CASE WHEN p.out_y <= c.two_digit_year_pivot THEN 2000 ELSE 1900 END
        END AS out_year
    FROM parsed p
    CROSS JOIN cfg_run c
),
dated AS (
    SELECT
        y.*,
        CASE WHEN y.in_open OR y.in_year IS NULL OR y.in_m IS NULL OR y.in_d IS NULL THEN NULL
             ELSE TRY_CAST(printf('%04d-%02d-%02d', y.in_year, y.in_m, y.in_d) AS DATE) END   AS date_in,
        CASE WHEN y.out_open OR y.out_year IS NULL OR y.out_m IS NULL OR y.out_d IS NULL THEN NULL
             ELSE TRY_CAST(printf('%04d-%02d-%02d', y.out_year, y.out_m, y.out_d) AS DATE) END AS date_out
    FROM years y
),
resolved AS (
    SELECT
        d.*,
        (d.in_open  OR d.date_in  IS NOT NULL) AS date_in_resolved,
        (d.out_open OR d.date_out IS NOT NULL) AS date_out_resolved,
        pf.raw IS NOT NULL                     AS pseudo_resolved,
        pf.is_pseudo                           AS is_pseudo_line,
        (d.bom_policy_mode = 'AllTypesUnfiltered'
            OR NULLIF(trim(d.STBKAR), '') IN (SELECT bom_type FROM cfg_bom_type)) AS included_by_policy
    FROM dated d
    LEFT JOIN cfg_pseudo_flag pf ON pf.raw = trim(d.STFIST)
)
SELECT
    'BL-' || substr(sha256(r._row_hash || ':' || CAST(r.dup_seq AS VARCHAR)), 1, 24) AS bom_line_id,
    NULLIF(trim(r.STANR), '')                                AS parent_item_id,
    NULLIF(trim(r.STANRU), '')                               AS child_item_id,
    NULLIF(trim(r.STBKAR), '')                               AS bom_type,
    NULLIF(trim(r.STBKPO), '')                               AS bom_position,
    NULLIF(trim(r.STMNRU), '')                               AS child_model_no,
    r.STMNG                                                  AS quantity_raw,
    TRY_CAST(NULLIF(trim(r.STMNG), '') AS DECIMAL(38, 10))   AS quantity,
    r.STFORM                                                 AS quantity_formula,
    NULLIF(trim(r.STMEIN), '')                               AS unit,
    TRY_CAST(NULLIF(trim(r.STAUSF), '') AS DECIMAL(38, 10))  AS scrap_factor,
    TRY_CAST(NULLIF(trim(r.STAUSM), '') AS DECIMAL(38, 10))  AS scrap_quantity,
    NULLIF(trim(r.STAGPO), '')                               AS routing_position,
    r.STFIST                                                 AS pseudo_raw,
    r.pseudo_resolved,
    r.is_pseudo_line,
    r.STABJJ AS date_in_year_raw,  r.STABMM AS date_in_month_raw,  r.STABTT AS date_in_day_raw,
    r.STBIJJ AS date_out_year_raw, r.STBIMM AS date_out_month_raw, r.STBITT AS date_out_day_raw,
    r.date_in,
    r.date_out,
    r.date_in_resolved,
    r.date_out_resolved,
    r.STTXLN                                                 AS additional_text,
    NULLIF(trim(r.STBDID), '')                               AS bom_condition_id,
    r.included_by_policy,
    CASE
        WHEN NOT (r.date_in_resolved AND r.date_out_resolved) THEN NULL
        ELSE (r.date_in IS NULL OR r.date_in <= r.effective_date)
         AND (r.date_out IS NULL
              OR (r.date_out_inclusive AND r.date_out >= r.effective_date)
              OR (NOT r.date_out_inclusive AND r.date_out > r.effective_date))
    END                                                      AS is_effective,
    r._row_hash                                              AS source_row_hash
FROM resolved r
ORDER BY parent_item_id, bom_type, bom_position, child_item_id, bom_line_id;

-- Effective occurrences. Lines with unresolved effectivity are kept (is_effective IS NULL) so the scope is
-- never silently reduced; they carry a blocking BOM_DATE_UNRESOLVED issue.
CREATE OR REPLACE TABLE effective_bom_line AS
SELECT
    *,
    is_effective IS NOT NULL AS effectivity_resolved
FROM bom_line
WHERE included_by_policy
  AND is_effective IS NOT FALSE
  AND parent_item_id IS NOT NULL
  AND child_item_id IS NOT NULL
ORDER BY parent_item_id, bom_type, bom_position, child_item_id, bom_line_id;

-- Distinct parent-child edges for traversal only; provenance via bom_edge_line.
CREATE OR REPLACE TABLE bom_edge AS
SELECT
    parent_item_id,
    child_item_id,
    count(*)                    AS line_count,
    bool_and(effectivity_resolved) AS all_lines_resolved
FROM effective_bom_line
GROUP BY parent_item_id, child_item_id
ORDER BY parent_item_id, child_item_id;

CREATE OR REPLACE TABLE bom_edge_line AS
SELECT parent_item_id, child_item_id, bom_line_id
FROM effective_bom_line
ORDER BY parent_item_id, child_item_id, bom_line_id;

-- Selectable BOMs (every GSST00 parent). Uses all source lines, like the BOM-scope explosion, so selection and
-- import scope agree; is_top = never a child on any line.
CREATE OR REPLACE TABLE bom_parent AS
WITH p AS (
    SELECT parent_item_id AS item_id, count(*) AS line_count, count(DISTINCT child_item_id) AS child_count
    FROM bom_line
    WHERE parent_item_id IS NOT NULL
    GROUP BY parent_item_id
),
c AS (
    SELECT DISTINCT child_item_id AS item_id FROM bom_line WHERE child_item_id IS NOT NULL
),
d AS (
    SELECT item_id, min(description_1) AS description FROM swing_item GROUP BY item_id
)
SELECT p.item_id, d.description, p.line_count, p.child_count, c.item_id IS NULL AS is_top
FROM p
LEFT JOIN c USING (item_id)
LEFT JOIN d USING (item_id)
ORDER BY p.item_id;

-- Candidate line key (STANR, STBKAR, STBKPO) profile; not enforced until uniqueness is confirmed.
CREATE OR REPLACE TABLE bom_line_key_profile AS
SELECT
    parent_item_id,
    bom_type,
    bom_position,
    count(*)                                  AS n_lines,
    count(DISTINCT child_item_id)             AS n_children,
    count(*) FILTER (WHERE is_effective)      AS n_effective
FROM bom_line
WHERE parent_item_id IS NOT NULL
GROUP BY parent_item_id, bom_type, bom_position
HAVING count(*) > 1
ORDER BY parent_item_id, bom_type, bom_position;

-- ---------------------------------------------------------------------------------------------
-- Source issues (folded into review_issues by IssuesStep).
-- ---------------------------------------------------------------------------------------------
CREATE OR REPLACE TABLE swing_source_issue (
    issue_type     VARCHAR,
    severity       VARCHAR,
    fix_source     VARCHAR,
    item_id        VARCHAR,
    parent_item_id VARCHAR,
    bom_line_id    VARCHAR,
    detail         VARCHAR
);

INSERT INTO swing_source_issue
SELECT 'BOM_LINE_KEY_MISSING', 'Block', 'SwingBom', child_item_id, parent_item_id, bom_line_id,
       'GSST00 line without parent (STANR) or child (STANRU) item number'
FROM bom_line
WHERE included_by_policy AND (parent_item_id IS NULL OR child_item_id IS NULL);

INSERT INTO swing_source_issue
SELECT 'BOM_DATE_UNRESOLVED', 'Block', 'SwingBom', child_item_id, parent_item_id, bom_line_id,
       concat_ws(' ',
           CASE WHEN NOT date_in_resolved
                THEN 'valid-from STABJJ/STABMM/STABTT=' || coalesce(date_in_year_raw, '<null>') || '/' || coalesce(date_in_month_raw, '<null>') || '/' || coalesce(date_in_day_raw, '<null>') END,
           CASE WHEN NOT date_out_resolved
                THEN 'valid-till STBIJJ/STBIMM/STBITT=' || coalesce(date_out_year_raw, '<null>') || '/' || coalesce(date_out_month_raw, '<null>') || '/' || coalesce(date_out_day_raw, '<null>') END)
FROM bom_line
WHERE included_by_policy
  AND parent_item_id IS NOT NULL AND child_item_id IS NOT NULL
  AND NOT (date_in_resolved AND date_out_resolved);

INSERT INTO swing_source_issue
SELECT 'PSEUDO_FLAG_UNKNOWN', 'Block', 'SwingBom', child_item_id, parent_item_id, bom_line_id,
       'STFIST value ''' || coalesce(pseudo_raw, '<null>') || ''' is not in the configured pseudo flag map'
FROM effective_bom_line
WHERE NOT pseudo_resolved;

INSERT INTO swing_source_issue
SELECT 'BOM_QUANTITY_UNPARSEABLE', 'Warn', 'SwingBom', child_item_id, parent_item_id, bom_line_id,
       'STMNG value ''' || quantity_raw || ''' is not a decimal'
FROM effective_bom_line
WHERE quantity IS NULL AND NULLIF(trim(quantity_raw), '') IS NOT NULL;

INSERT INTO swing_source_issue
WITH bom_items AS (
    SELECT parent_item_id AS item_id FROM effective_bom_line
    UNION
    SELECT child_item_id FROM effective_bom_line
)
SELECT 'ITEM_TYPE_UNKNOWN', 'Block', 'SwingItemMaster', b.item_id, NULL, NULL,
       CASE WHEN t.item_id IS NULL THEN 'No GSAR00 record for item (ARANR)'
            WHEN t.source_rows = 0 THEN 'No GSAR00 base record (ARMONR blank); only ' || t.model_count || ' model row(s)'
            ELSE 'Unsupported GSAR00.ARVAKZ value ''' || t.variant_flags_raw || ''' on the base record (ARMONR blank)' END
FROM bom_items b
LEFT JOIN swing_item_type t ON t.item_id = b.item_id
WHERE t.item_id IS NULL OR (t.source_type = 'UNKNOWN' AND NOT t.is_ambiguous);

INSERT INTO swing_source_issue
WITH bom_items AS (
    SELECT parent_item_id AS item_id FROM effective_bom_line
    UNION
    SELECT child_item_id FROM effective_bom_line
)
SELECT 'ITEM_MODEL_AMBIGUOUS', 'Block', 'SwingItemMaster', t.item_id, NULL, NULL,
       'GSAR00 has ' || t.source_rows || ' base rows (ARMONR blank) with different ARVAKZ values: ' || t.variant_flags_raw
FROM swing_item_type t
JOIN bom_items b ON b.item_id = t.item_id
WHERE t.is_ambiguous;

INSERT INTO swing_source_issue
SELECT 'BOM_CHILD_MODEL_NOT_FOUND', 'Review', 'SwingBom', l.child_item_id, l.parent_item_id, l.bom_line_id,
       'STMNRU model ''' || l.child_model_no || ''' not found for item in GSAR00 (ARANR + ARMONR)'
FROM effective_bom_line l
WHERE l.child_model_no IS NOT NULL
  AND EXISTS     (SELECT 1 FROM swing_item i WHERE i.item_id = l.child_item_id)
  AND NOT EXISTS (SELECT 1 FROM swing_item i WHERE i.item_id = l.child_item_id AND i.model_no = l.child_model_no);

INSERT INTO swing_source_issue
SELECT 'BOM_LINE_KEY_NOT_UNIQUE', 'Info', 'SwingBom', NULL, parent_item_id, NULL,
       'Candidate key (STANR, STBKAR, STBKPO) = (' || parent_item_id || ', ' || coalesce(bom_type, '<null>') || ', ' || coalesce(bom_position, '<null>')
       || ') occurs on ' || n_lines || ' lines (' || n_children || ' children, ' || n_effective || ' effective)'
FROM bom_line_key_profile;
