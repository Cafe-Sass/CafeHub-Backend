LOCK TABLE tenant_users IN SHARE ROW EXCLUSIVE MODE;

CREATE TEMP TABLE cafehub_email_backfill ON COMMIT DROP AS
SELECT id, email, lower(btrim(email)) AS normalized_email
FROM tenant_users
WHERE user_kind IN ('Owner', 'Staff') OR email IS NOT NULL OR normalized_email IS NOT NULL;

DO $cafehub_email$
DECLARE
    snapshot_proof text;
BEGIN
    IF EXISTS (
        SELECT 1 FROM cafehub_email_backfill
        WHERE normalized_email IS NULL OR char_length(email) > 254
            OR strpos(normalized_email, '@') <= 1
            OR strpos(normalized_email, '@') = char_length(normalized_email)
            OR length(normalized_email) - length(replace(normalized_email, '@', '')) <> 1
    ) THEN
        RAISE EXCEPTION 'Resolve unusable tenant user emails before migration.';
    END IF;

    IF EXISTS (
        SELECT 1 FROM cafehub_email_backfill
        GROUP BY normalized_email HAVING count(*) > 1
    ) THEN
        RAISE EXCEPTION 'Resolve duplicate tenant user emails before migration.';
    END IF;

    SELECT encode(sha256(string_agg(
        sha256(convert_to(id::text || email, 'UTF8') || decode('00', 'hex') || convert_to(normalized_email, 'UTF8')),
        ''::bytea ORDER BY id)), 'hex')
    INTO snapshot_proof
    FROM cafehub_email_backfill;

    IF snapshot_proof IS NOT NULL AND snapshot_proof IS DISTINCT FROM
        current_setting('cafehub.tenant_email_backfill_proof', true) THEN
        RAISE EXCEPTION 'The tenant email inventory is invalid, changed, or differs from sign-in normalization.';
    END IF;
END $cafehub_email$;

UPDATE tenant_users u
SET normalized_email = b.normalized_email
FROM cafehub_email_backfill b
WHERE u.id = b.id AND convert_to(u.normalized_email, 'UTF8') IS DISTINCT FROM convert_to(b.normalized_email, 'UTF8');

DROP INDEX ix_tenant_users_normalized_email;
CREATE UNIQUE INDEX uq_tenant_users_normalized_email ON tenant_users (normalized_email)
    WHERE normalized_email IS NOT NULL;
