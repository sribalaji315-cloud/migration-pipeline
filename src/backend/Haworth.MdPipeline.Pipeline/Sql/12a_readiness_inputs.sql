-- Step 12 inputs from SQLite (appended from C# before 12_readiness.sql).
CREATE OR REPLACE TABLE cfg_type_decision (item_id VARCHAR, decision VARCHAR, inherited_attrs VARCHAR, swing_type_code VARCHAR);
CREATE OR REPLACE TABLE cfg_exported_item (item_id VARCHAR);
