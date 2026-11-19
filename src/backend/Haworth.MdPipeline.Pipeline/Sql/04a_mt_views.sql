-- Step 4 (part 1): Migration Tool Bronze views with fixed templates so older snapshots (fewer/other columns) and
-- snapshots without Migration Tool data stay readable. 04b/04c replace the empty views when files exist.
CREATE OR REPLACE TABLE tpl_mt_mapping (
    legacy_item_id VARCHAR, legacy_feature_id VARCHAR, legacy_value VARCHAR, new_attribute_id VARCHAR, new_value VARCHAR,
    attribute_type VARCHAR, condition VARCHAR, simplified_legacy_condition VARCHAR, target_condition VARCHAR,
    mapped_from VARCHAR, value_status VARCHAR, feasibility VARCHAR, legacy_reachable BOOLEAN, inherited VARCHAR,
    prediction_status VARCHAR, prediction_confidence VARCHAR, updated_at TIMESTAMP
);
CREATE OR REPLACE TABLE tpl_mt_item (
    item_id VARCHAR, description VARCHAR, category VARCHAR, product_type VARCHAR, priority VARCHAR, approved_for_migration BOOLEAN
);
CREATE OR REPLACE VIEW mt_mappings AS SELECT * FROM tpl_mt_mapping;
CREATE OR REPLACE VIEW mt_items AS SELECT * FROM tpl_mt_item;
