\set ON_ERROR_STOP on
START TRANSACTION;

\if :{?email_proof}
SELECT set_config('cafehub.tenant_email_backfill_proof', :'email_proof', true) IS NOT NULL AS proof_loaded;
\endif
\ir NormalizeTenantUserEmail.Backfill.sql

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261115160000_NormalizeTenantUserEmail', '10.0.13');

COMMIT;
