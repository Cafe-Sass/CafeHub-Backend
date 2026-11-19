-- Run after Expand with the legacy station column still present. Read-only.
SELECT "MigrationId", "ProductVersion"
FROM "__EFMigrationsHistory"
ORDER BY "MigrationId";

SELECT COUNT(*) AS tags_needing_backfill_or_review
FROM product_tags
WHERE id IS NULL OR id = '00000000-0000-0000-0000-000000000000'
   OR tag <> btrim(tag) OR btrim(tag) = '';

SELECT product_id, btrim(tag) AS proposed_normalized_label, COUNT(*) AS active_label_count
FROM product_tags
WHERE status = 'Active'
GROUP BY product_id, btrim(tag)
HAVING COUNT(*) > 1;

SELECT p.id AS product_id,
       COUNT(ps.id) FILTER (WHERE ps.status = 'Active') AS active_sizes,
       COUNT(ps.id) FILTER (WHERE ps.status = 'Active' AND ps.is_default) AS active_default_sizes
FROM products AS p
LEFT JOIN product_sizes AS ps ON ps.product_id = p.id
GROUP BY p.id
HAVING COUNT(ps.id) FILTER (WHERE ps.status = 'Active') = 0
    OR COUNT(ps.id) FILTER (WHERE ps.status = 'Active' AND ps.is_default) <> 1
ORDER BY p.id;

SELECT p.id AS product_id, p.station_id AS legacy_station_id,
       s.branch_id AS candidate_branch_id, b.lifecycle_status AS branch_status,
       s.status AS station_status, a.station_id AS current_station_id,
       CASE
           WHEN p.station_id IS NULL THEN 'NO_LEGACY_CANDIDATE'
           WHEN b.id IS NULL THEN 'UNRESOLVED_LEGACY_REFERENCE'
           WHEN a.id IS NULL THEN 'CANDIDATE_MAPPING_MISSING'
           WHEN a.station_id <> s.id THEN 'CANDIDATE_MAPPING_DIFFERS'
           ELSE 'CANDIDATE_MAPPING_MATCHES'
       END AS result
FROM products AS p
LEFT JOIN stations AS s ON s.id = p.station_id
LEFT JOIN branches AS b ON b.id = s.branch_id
LEFT JOIN product_station_assignments AS a
  ON a.product_id = p.id AND a.branch_id = s.branch_id AND a.ended_at IS NULL
ORDER BY p.id;

-- An approved branch/product expectation is required to certify true absence.
-- The preceding candidate report must not be used as proof of an unassigned branch.
SELECT EXISTS (SELECT 1 FROM product_routing_readiness) AS routing_enabled;
