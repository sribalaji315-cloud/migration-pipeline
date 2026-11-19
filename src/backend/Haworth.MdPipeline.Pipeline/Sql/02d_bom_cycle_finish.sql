-- Step 2c cycle detection (finish): persist cyclic edges and raise blocking issues.
CREATE OR REPLACE TABLE bom_cycle_edge AS
SELECT parent_item_id, child_item_id FROM bom_cycle_work ORDER BY parent_item_id, child_item_id;

DROP TABLE bom_cycle_work;

INSERT INTO swing_source_issue
SELECT 'BOM_CYCLE', 'Block', 'SwingBom', child_item_id, parent_item_id, NULL,
       'Effective GSST00 edge ' || parent_item_id || ' -> ' || child_item_id || ' lies on or between BOM cycles'
FROM bom_cycle_edge;
