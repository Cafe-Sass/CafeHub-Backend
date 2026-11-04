START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104145404_AddTenantSchemaUpgradeTracking') THEN
    ALTER TABLE tenant_databases ADD schema_migration_id text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104145404_AddTenantSchemaUpgradeTracking') THEN
    CREATE TABLE tenant_schema_upgrade_runs (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        target_migration_id text NOT NULL,
        status text NOT NULL,
        failure_code text,
        attempt_count integer NOT NULL,
        requested_by uuid,
        started_at timestamptz,
        finished_at timestamptz,
        lease_expires_at timestamptz,
        leased_by text,
        created_at timestamptz NOT NULL,
        updated_at timestamptz NOT NULL,
        created_by uuid,
        updated_by uuid,
        CONSTRAINT pk_tenant_schema_upgrade_runs PRIMARY KEY (id),
        CONSTRAINT ck_tenant_schema_upgrade_runs_attempt_count_not_negative CHECK (attempt_count >= 0),
        CONSTRAINT ck_tenant_schema_upgrade_runs_failure_code_length CHECK (failure_code IS NULL OR (length(failure_code) >= 1 AND length(failure_code) <= 64)),
        CONSTRAINT ck_tenant_schema_upgrade_runs_status CHECK (status IN ('Pending', 'Running', 'Succeeded', 'Failed')),
        CONSTRAINT ck_tenant_schema_upgrade_runs_target_migration_id_length CHECK (length(target_migration_id) >= 1 AND length(target_migration_id) <= 150),
        CONSTRAINT fk_tenant_schema_upgrade_runs_created_by FOREIGN KEY (created_by) REFERENCES platform_admins (id) ON DELETE RESTRICT,
        CONSTRAINT fk_tenant_schema_upgrade_runs_requested_by FOREIGN KEY (requested_by) REFERENCES platform_admins (id) ON DELETE RESTRICT,
        CONSTRAINT fk_tenant_schema_upgrade_runs_tenant_id FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE RESTRICT,
        CONSTRAINT fk_tenant_schema_upgrade_runs_updated_by FOREIGN KEY (updated_by) REFERENCES platform_admins (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104145404_AddTenantSchemaUpgradeTracking') THEN
    ALTER TABLE tenant_databases ADD CONSTRAINT ck_tenant_databases_schema_migration_id_length CHECK (schema_migration_id IS NULL OR (length(schema_migration_id) >= 1 AND length(schema_migration_id) <= 150));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104145404_AddTenantSchemaUpgradeTracking') THEN
    CREATE INDEX "IX_tenant_schema_upgrade_runs_created_by" ON tenant_schema_upgrade_runs (created_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104145404_AddTenantSchemaUpgradeTracking') THEN
    CREATE INDEX "IX_tenant_schema_upgrade_runs_requested_by" ON tenant_schema_upgrade_runs (requested_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104145404_AddTenantSchemaUpgradeTracking') THEN
    CREATE INDEX ix_tenant_schema_upgrade_runs_tenant_id ON tenant_schema_upgrade_runs (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104145404_AddTenantSchemaUpgradeTracking') THEN
    CREATE INDEX "IX_tenant_schema_upgrade_runs_updated_by" ON tenant_schema_upgrade_runs (updated_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104145404_AddTenantSchemaUpgradeTracking') THEN
    CREATE UNIQUE INDEX uq_tenant_schema_upgrade_runs_active ON tenant_schema_upgrade_runs (tenant_id) WHERE status IN ('Pending', 'Running');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104145404_AddTenantSchemaUpgradeTracking') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261104145404_AddTenantSchemaUpgradeTracking', '10.0.13');
    END IF;
END $EF$;
COMMIT;

