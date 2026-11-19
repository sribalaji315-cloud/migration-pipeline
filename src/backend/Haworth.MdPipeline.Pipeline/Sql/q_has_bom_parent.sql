-- API check: does this Silver contain bom_parent (added after the first release)?
SELECT count(*) FROM information_schema.tables WHERE table_name = 'bom_parent';
