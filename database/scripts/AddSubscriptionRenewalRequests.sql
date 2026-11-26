START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261126203130_AddSubscriptionRenewalRequests') THEN
    CREATE TABLE subscription_renewal_requests (
        id uuid NOT NULL,
        subscription_id uuid NOT NULL,
        plan_version_id uuid NOT NULL,
        proposed_price numeric(12,2) NOT NULL,
        proposed_currency_code text NOT NULL,
        status text NOT NULL,
        requested_at timestamptz NOT NULL,
        expires_at timestamptz NOT NULL,
        decided_at timestamptz,
        decided_by text,
        created_at timestamptz NOT NULL,
        updated_at timestamptz NOT NULL,
        created_by uuid,
        updated_by uuid,
        CONSTRAINT pk_subscription_renewal_requests PRIMARY KEY (id),
        CONSTRAINT ck_subscription_renewal_requests_status CHECK (status IN ('Pending', 'Approved', 'Declined', 'Expired', 'Completed')),
        CONSTRAINT ck_subscription_renewal_requests_price_non_negative CHECK (proposed_price >= 0),
        CONSTRAINT ck_subscription_renewal_requests_currency_code_format CHECK (proposed_currency_code ~ '^[a-z]{3}$'),
        CONSTRAINT ck_subscription_renewal_requests_expiry CHECK (expires_at > requested_at),
        CONSTRAINT ck_subscription_renewal_requests_decided_by CHECK (decided_by IS NULL OR char_length(decided_by) BETWEEN 1 AND 256),
        CONSTRAINT ck_subscription_renewal_requests_decided CHECK ((status = 'Pending') = (decided_at IS NULL)),
        CONSTRAINT fk_subscription_renewal_requests_subscription_id FOREIGN KEY (subscription_id) REFERENCES subscriptions (id) ON DELETE RESTRICT,
        CONSTRAINT fk_subscription_renewal_requests_plan_version_id FOREIGN KEY (plan_version_id) REFERENCES plan_versions (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261126203130_AddSubscriptionRenewalRequests') THEN
    CREATE INDEX ix_subscription_renewal_requests_plan_version_id ON subscription_renewal_requests (plan_version_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261126203130_AddSubscriptionRenewalRequests') THEN
    CREATE INDEX ix_subscription_renewal_requests_subscription_requested_at ON subscription_renewal_requests (subscription_id, requested_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261126203130_AddSubscriptionRenewalRequests') THEN
    CREATE UNIQUE INDEX uq_subscription_renewal_requests_open ON subscription_renewal_requests (subscription_id) WHERE status IN ('Pending', 'Approved');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261126203130_AddSubscriptionRenewalRequests') THEN
    ALTER TABLE platform_payment_transactions ADD CONSTRAINT fk_platform_payment_transactions_renewal_request_id FOREIGN KEY (renewal_request_id) REFERENCES subscription_renewal_requests (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261126203130_AddSubscriptionRenewalRequests') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261126203130_AddSubscriptionRenewalRequests', '10.0.13');
    END IF;
END $EF$;

COMMIT;
