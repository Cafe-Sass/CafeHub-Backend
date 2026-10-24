CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024000514_CreateMasterIdentity') THEN
    CREATE TABLE platform_admins (
        id uuid NOT NULL,
        deleted_at timestamptz,
        deleted_by uuid,
        created_at timestamptz NOT NULL,
        updated_at timestamptz NOT NULL,
        created_by uuid,
        updated_by uuid,
        user_name text NOT NULL,
        normalized_user_name text NOT NULL,
        email text,
        normalized_email text,
        email_confirmed boolean NOT NULL DEFAULT FALSE,
        password_hash text NOT NULL,
        security_stamp text NOT NULL,
        concurrency_stamp text NOT NULL,
        phone_number text,
        phone_number_confirmed boolean NOT NULL DEFAULT FALSE,
        two_factor_enabled boolean NOT NULL DEFAULT FALSE,
        lockout_end_at timestamptz,
        lockout_enabled boolean NOT NULL DEFAULT TRUE,
        access_failed_count integer NOT NULL DEFAULT 0,
        CONSTRAINT pk_platform_admins PRIMARY KEY (id),
        CONSTRAINT ck_platform_admins_concurrency_stamp_length CHECK (length(concurrency_stamp) >= 1 AND length(concurrency_stamp) <= 64),
        CONSTRAINT ck_platform_admins_email_length CHECK (email IS NULL OR (length(email) >= 1 AND length(email) <= 320)),
        CONSTRAINT ck_platform_admins_normalized_email_length CHECK (normalized_email IS NULL OR (length(normalized_email) >= 1 AND length(normalized_email) <= 320)),
        CONSTRAINT ck_platform_admins_normalized_user_name_length CHECK (length(normalized_user_name) >= 1 AND length(normalized_user_name) <= 256),
        CONSTRAINT ck_platform_admins_password_hash_length CHECK (length(password_hash) >= 1 AND length(password_hash) <= 512),
        CONSTRAINT ck_platform_admins_phone_number_length CHECK (phone_number IS NULL OR (length(phone_number) >= 1 AND length(phone_number) <= 64)),
        CONSTRAINT ck_platform_admins_security_stamp_length CHECK (length(security_stamp) >= 1 AND length(security_stamp) <= 64),
        CONSTRAINT ck_platform_admins_user_name_length CHECK (length(user_name) >= 1 AND length(user_name) <= 256),
        CONSTRAINT fk_platform_admins_created_by FOREIGN KEY (created_by) REFERENCES platform_admins (id) ON DELETE RESTRICT,
        CONSTRAINT fk_platform_admins_deleted_by FOREIGN KEY (deleted_by) REFERENCES platform_admins (id) ON DELETE RESTRICT,
        CONSTRAINT fk_platform_admins_updated_by FOREIGN KEY (updated_by) REFERENCES platform_admins (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024000514_CreateMasterIdentity') THEN
    CREATE TABLE platform_admin_roles (
        id uuid NOT NULL,
        created_at timestamptz NOT NULL,
        updated_at timestamptz NOT NULL,
        created_by uuid,
        updated_by uuid,
        name text NOT NULL,
        normalized_name text NOT NULL,
        concurrency_stamp text NOT NULL,
        CONSTRAINT pk_platform_admin_roles PRIMARY KEY (id),
        CONSTRAINT ck_platform_admin_roles_concurrency_stamp_length CHECK (length(concurrency_stamp) >= 1 AND length(concurrency_stamp) <= 64),
        CONSTRAINT ck_platform_admin_roles_name_length CHECK (length(name) >= 1 AND length(name) <= 256),
        CONSTRAINT ck_platform_admin_roles_normalized_name_length CHECK (length(normalized_name) >= 1 AND length(normalized_name) <= 256),
        CONSTRAINT fk_platform_admin_roles_created_by FOREIGN KEY (created_by) REFERENCES platform_admins (id) ON DELETE RESTRICT,
        CONSTRAINT fk_platform_admin_roles_updated_by FOREIGN KEY (updated_by) REFERENCES platform_admins (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024000514_CreateMasterIdentity') THEN
    CREATE TABLE platform_admin_user_claims (
        id uuid NOT NULL,
        created_at timestamptz NOT NULL,
        platform_admin_id uuid NOT NULL,
        claim_type text NOT NULL,
        claim_value text,
        CONSTRAINT pk_platform_admin_user_claims PRIMARY KEY (id),
        CONSTRAINT ck_platform_admin_user_claims_claim_type_length CHECK (length(claim_type) >= 1 AND length(claim_type) <= 512),
        CONSTRAINT ck_platform_admin_user_claims_claim_value_length CHECK (claim_value IS NULL OR (length(claim_value) >= 0 AND length(claim_value) <= 1024)),
        CONSTRAINT fk_platform_admin_user_claims_platform_admin_id FOREIGN KEY (platform_admin_id) REFERENCES platform_admins (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024000514_CreateMasterIdentity') THEN
    CREATE TABLE platform_admin_role_claims (
        id uuid NOT NULL,
        created_at timestamptz NOT NULL,
        role_id uuid NOT NULL,
        claim_type text NOT NULL,
        claim_value text,
        CONSTRAINT pk_platform_admin_role_claims PRIMARY KEY (id),
        CONSTRAINT ck_platform_admin_role_claims_claim_type_length CHECK (length(claim_type) >= 1 AND length(claim_type) <= 512),
        CONSTRAINT ck_platform_admin_role_claims_claim_value_length CHECK (claim_value IS NULL OR (length(claim_value) >= 0 AND length(claim_value) <= 1024)),
        CONSTRAINT fk_platform_admin_role_claims_role_id FOREIGN KEY (role_id) REFERENCES platform_admin_roles (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024000514_CreateMasterIdentity') THEN
    CREATE TABLE platform_admin_user_roles (
        id uuid NOT NULL,
        created_at timestamptz NOT NULL,
        platform_admin_id uuid NOT NULL,
        role_id uuid NOT NULL,
        CONSTRAINT pk_platform_admin_user_roles PRIMARY KEY (id),
        CONSTRAINT fk_platform_admin_user_roles_platform_admin_id FOREIGN KEY (platform_admin_id) REFERENCES platform_admins (id) ON DELETE CASCADE,
        CONSTRAINT fk_platform_admin_user_roles_role_id FOREIGN KEY (role_id) REFERENCES platform_admin_roles (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024000514_CreateMasterIdentity') THEN
    CREATE INDEX ix_platform_admin_role_claims_role_id ON platform_admin_role_claims (role_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024000514_CreateMasterIdentity') THEN
    CREATE INDEX ix_platform_admin_roles_created_by ON platform_admin_roles (created_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024000514_CreateMasterIdentity') THEN
    CREATE INDEX ix_platform_admin_roles_updated_by ON platform_admin_roles (updated_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024000514_CreateMasterIdentity') THEN
    CREATE UNIQUE INDEX uq_platform_admin_roles_normalized_name ON platform_admin_roles (normalized_name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024000514_CreateMasterIdentity') THEN
    CREATE INDEX ix_platform_admin_user_claims_platform_admin_id ON platform_admin_user_claims (platform_admin_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024000514_CreateMasterIdentity') THEN
    CREATE INDEX ix_platform_admin_user_roles_role_id ON platform_admin_user_roles (role_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024000514_CreateMasterIdentity') THEN
    CREATE UNIQUE INDEX uq_platform_admin_user_roles_user_role ON platform_admin_user_roles (platform_admin_id, role_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024000514_CreateMasterIdentity') THEN
    CREATE INDEX ix_platform_admins_created_by ON platform_admins (created_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024000514_CreateMasterIdentity') THEN
    CREATE INDEX ix_platform_admins_deleted_by ON platform_admins (deleted_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024000514_CreateMasterIdentity') THEN
    CREATE INDEX ix_platform_admins_updated_by ON platform_admins (updated_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024000514_CreateMasterIdentity') THEN
    CREATE UNIQUE INDEX uq_platform_admins_normalized_email ON platform_admins (normalized_email) WHERE normalized_email IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024000514_CreateMasterIdentity') THEN
    CREATE UNIQUE INDEX uq_platform_admins_normalized_user_name ON platform_admins (normalized_user_name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024000514_CreateMasterIdentity') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261024000514_CreateMasterIdentity', '10.0.13');
    END IF;
END $EF$;
COMMIT;

