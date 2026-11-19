-- Step 2c cycle detection (init). Peeling repeatedly removes edges that start at a root or end at a leaf;
-- the remaining edges lie on or between BOM cycles.
CREATE OR REPLACE TABLE bom_cycle_work AS
SELECT parent_item_id, child_item_id FROM bom_edge;
