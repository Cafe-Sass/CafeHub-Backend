BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;

WITH checks AS (
    SELECT
        current_database() AS database_name,
        current_setting('server_version') AS server_version,
        current_setting('server_encoding') AS server_encoding,
        pg_collation_for('Cafe'::text) AS expression_collation,
        d.datlocprovider AS locale_provider,
        d.datcollate AS lc_collate,
        d.datctype AS lc_ctype,
        COALESCE(to_jsonb(d)->>'datlocale', to_jsonb(d)->>'daticulocale') AS provider_locale,
        d.datcollversion AS recorded_collation_version,
        pg_database_collation_actual_version(d.oid) AS actual_collation_version,
        lower('COFFEE') = lower('Coffee') AND lower('I') = 'i' AS ascii_case_matches,
        lower(U&'CAF\00C9') = lower(U&'Caf\00E9') AS accented_case_matches,
        lower(U&'Caf\00E9') <> lower('Cafe') AS accent_preserved,
        lower(U&'\00C9') <> lower(U&'E\0301') AS composed_forms_distinct,
        lower(U&'\0642\0647\0648\0629') = U&'\0642\0647\0648\0629' AS arabic_preserved,
        char_length(U&'\+01F600') = 1 AND char_length(U&'e\0301') = 2 AS unicode_scalar_length
    FROM pg_catalog.pg_database AS d
    WHERE d.datname = current_database()
)
SELECT
    *,
    server_encoding = 'UTF8'
        AND recorded_collation_version IS NOT DISTINCT FROM actual_collation_version
        AND ascii_case_matches
        AND accented_case_matches
        AND accent_preserved
        AND composed_forms_distinct
        AND arabic_preserved
        AND unicode_scalar_length AS preflight_pass
FROM checks;

COMMIT;
