START TRANSACTION;

CREATE TABLE wifi_settings (
    id uuid NOT NULL,
    policy text NOT NULL DEFAULT 'Free',
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid NULL,
    updated_by uuid NULL,
    CONSTRAINT pk_wifi_settings PRIMARY KEY (id),
    CONSTRAINT ck_wifi_settings_policy CHECK (policy IN ('Free', 'Paid')),
    CONSTRAINT fk_wifi_settings_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_wifi_settings_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE TABLE branch_wifi_networks (
    branch_id uuid NOT NULL,
    ssid text NOT NULL,
    security text NOT NULL,
    password text NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid NULL,
    updated_by uuid NULL,
    CONSTRAINT pk_branch_wifi_networks PRIMARY KEY (branch_id),
    CONSTRAINT ck_branch_wifi_networks_ssid CHECK (char_length(ssid) BETWEEN 1 AND 32 AND ssid !~ '[[:cntrl:]]'),
    CONSTRAINT ck_branch_wifi_networks_security CHECK (security IN ('None', 'WPA', 'WEP')),
    CONSTRAINT ck_branch_wifi_networks_password CHECK (password IS NULL OR (char_length(password) BETWEEN 1 AND 63 AND password !~ '[[:cntrl:]]')),
    CONSTRAINT fk_branch_wifi_networks_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_branch_wifi_networks_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_branch_wifi_networks_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_wifi_settings_created_by ON wifi_settings (created_by);

CREATE INDEX ix_wifi_settings_updated_by ON wifi_settings (updated_by);

CREATE UNIQUE INDEX uq_wifi_settings_singleton ON wifi_settings ((true));

CREATE INDEX ix_branch_wifi_networks_created_by ON branch_wifi_networks (created_by);

CREATE INDEX ix_branch_wifi_networks_updated_by ON branch_wifi_networks (updated_by);

INSERT INTO wifi_settings (id, policy, created_at, updated_at, created_by, updated_by)
VALUES ('1b3c5d7e-9f20-4a6b-8c1d-2e3f4a5b6c7d', 'Free', '2026-11-24T00:00:00Z', '2026-11-24T00:00:00Z', NULL, NULL)
ON CONFLICT (id) DO NOTHING;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261124145104_AddWifiSettings', '10.0.13');

COMMIT;
