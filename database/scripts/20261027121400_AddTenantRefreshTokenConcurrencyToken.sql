START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027121400_AddTenantRefreshTokenConcurrencyToken') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261027121400_AddTenantRefreshTokenConcurrencyToken', '10.0.13');
    END IF;
END $EF$;
COMMIT;
