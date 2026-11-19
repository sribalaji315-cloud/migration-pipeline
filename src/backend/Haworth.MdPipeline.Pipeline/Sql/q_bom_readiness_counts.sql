-- API query: BOM readiness counts per status (overview).
SELECT status, count(*) AS boms FROM bom_readiness GROUP BY status ORDER BY status;
