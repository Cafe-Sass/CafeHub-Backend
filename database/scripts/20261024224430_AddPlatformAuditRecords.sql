START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024224430_AddPlatformAuditRecords') THEN
    CREATE TABLE platform_audit_records (
        id uuid NOT NULL,
        recorded_at timestamptz NOT NULL,
        actor_platform_admin_id uuid,
        actor_kind text NOT NULL,
        action_code text NOT NULL,
        subject_type text NOT NULL,
        subject_id uuid,
        before_state jsonb,
        after_state jsonb,
        CONSTRAINT pk_platform_audit_records PRIMARY KEY (id),
        CONSTRAINT ck_platform_audit_records_action_code_length CHECK (length(action_code) >= 1 AND length(action_code) <= 128),
        CONSTRAINT ck_platform_audit_records_kind CHECK (actor_kind IN ('PlatformAdmin', 'System')),
        CONSTRAINT ck_platform_audit_records_subject_type_length CHECK (length(subject_type) >= 1 AND length(subject_type) <= 64),
        CONSTRAINT fk_platform_audit_records_actor_platform_admin_id FOREIGN KEY (actor_platform_admin_id) REFERENCES platform_admins (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024224430_AddPlatformAuditRecords') THEN
    CREATE INDEX ix_platform_audit_records_actor_platform_admin_id ON platform_audit_records (actor_platform_admin_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024224430_AddPlatformAuditRecords') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261024224430_AddPlatformAuditRecords', '10.0.13');
    END IF;
END $EF$;
COMMIT;

