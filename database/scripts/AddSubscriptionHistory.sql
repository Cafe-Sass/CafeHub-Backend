START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261126193821_AddSubscriptionHistory') THEN
    CREATE TABLE subscription_history (
        id uuid NOT NULL,
        subscription_id uuid NOT NULL,
        from_status text,
        to_status text NOT NULL,
        plan_version_id uuid,
        period_number integer,
        actor_platform_admin_id uuid,
        actor_reference text,
        reason text,
        occurred_at timestamptz NOT NULL,
        created_at timestamptz NOT NULL,
        CONSTRAINT pk_subscription_history PRIMARY KEY (id),
        CONSTRAINT ck_subscription_history_from_status CHECK (from_status IS NULL OR from_status IN ('Active', 'Suspended', 'Expired', 'Cancelled')),
        CONSTRAINT ck_subscription_history_to_status CHECK (to_status IN ('Active', 'Suspended', 'Expired', 'Cancelled')),
        CONSTRAINT ck_subscription_history_period_number CHECK (period_number IS NULL OR period_number >= 1),
        CONSTRAINT ck_subscription_history_actor_reference CHECK (actor_reference IS NULL OR (char_length(actor_reference) BETWEEN 1 AND 256 AND actor_reference !~ '[[:cntrl:]]')),
        CONSTRAINT ck_subscription_history_reason CHECK (reason IS NULL OR (char_length(reason) BETWEEN 1 AND 256 AND reason !~ '[[:cntrl:]]')),
        CONSTRAINT fk_subscription_history_subscription_id FOREIGN KEY (subscription_id) REFERENCES subscriptions (id) ON DELETE RESTRICT,
        CONSTRAINT fk_subscription_history_plan_version_id FOREIGN KEY (plan_version_id) REFERENCES plan_versions (id) ON DELETE RESTRICT,
        CONSTRAINT fk_subscription_history_actor_platform_admin_id FOREIGN KEY (actor_platform_admin_id) REFERENCES platform_admins (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261126193821_AddSubscriptionHistory') THEN
    CREATE INDEX ix_subscription_history_subscription_occurred_at ON subscription_history (subscription_id, occurred_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261126193821_AddSubscriptionHistory') THEN
    CREATE INDEX ix_subscription_history_plan_version_id ON subscription_history (plan_version_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261126193821_AddSubscriptionHistory') THEN
    CREATE INDEX ix_subscription_history_actor_platform_admin_id ON subscription_history (actor_platform_admin_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261126193821_AddSubscriptionHistory') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261126193821_AddSubscriptionHistory', '10.0.13');
    END IF;
END $EF$;

COMMIT;
