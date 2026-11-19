-- Step 2c cycle detection (one peel iteration; repeated from C# until no edge is removed).
DELETE FROM bom_cycle_work
WHERE parent_item_id NOT IN (SELECT child_item_id FROM bom_cycle_work)
   OR child_item_id  NOT IN (SELECT parent_item_id FROM bom_cycle_work);
