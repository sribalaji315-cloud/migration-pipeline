-- API query: Swing source issue counts for the overview.
SELECT issue_type, severity, fix_source, count(*) AS issue_count
FROM swing_source_issue
GROUP BY issue_type, severity, fix_source
ORDER BY CASE severity WHEN 'Block' THEN 1 WHEN 'Review' THEN 2 WHEN 'Approve' THEN 3 WHEN 'Warn' THEN 4 ELSE 5 END, issue_type;
