START TRANSACTION;

CREATE TABLE cafe_payment_settings (
    id uuid NOT NULL,
    online_enabled boolean NOT NULL DEFAULT false,
    method_label text NOT NULL DEFAULT 'InstaPay',
    payment_link text NULL,
    payment_address text NULL,
    display_name text NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid,
    updated_by uuid,
    CONSTRAINT pk_cafe_payment_settings PRIMARY KEY (id),
    CONSTRAINT ck_cafe_payment_settings_enable_needs_destination CHECK (NOT online_enabled OR payment_link IS NOT NULL OR payment_address IS NOT NULL),
    CONSTRAINT ck_cafe_payment_settings_method_label_length CHECK (length(method_label) >= 1 AND length(method_label) <= 32),
    CONSTRAINT ck_cafe_payment_settings_payment_link_format CHECK (payment_link IS NULL OR (payment_link ~ '^https://' AND length(payment_link) <= 512)),
    CONSTRAINT ck_cafe_payment_settings_payment_address_format CHECK (payment_address IS NULL OR (length(payment_address) >= 1 AND length(payment_address) <= 128 AND payment_address !~ '[[:cntrl:]]')),
    CONSTRAINT ck_cafe_payment_settings_display_name_format CHECK (display_name IS NULL OR (length(display_name) >= 1 AND length(display_name) <= 128 AND display_name !~ '[[:cntrl:]]')),
    CONSTRAINT fk_cafe_payment_settings_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_cafe_payment_settings_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_cafe_payment_settings_created_by ON cafe_payment_settings (created_by);

CREATE INDEX ix_cafe_payment_settings_updated_by ON cafe_payment_settings (updated_by);

CREATE UNIQUE INDEX uq_cafe_payment_settings_singleton ON cafe_payment_settings ((true));

INSERT INTO cafe_payment_settings (id, online_enabled, method_label, payment_link, payment_address, display_name, created_at, updated_at, created_by, updated_by)
VALUES ('0a4d5f31-6c2e-4b7a-9f8d-1e2c3b4a5d6f', false, 'InstaPay', NULL, NULL, NULL, now(), now(), NULL, NULL)
ON CONFLICT (id) DO NOTHING;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261123225318_AddCafePaymentSettings', '10.0.13');

COMMIT;
