START TRANSACTION;

CREATE TABLE wifi_packages (
    id uuid NOT NULL,
    name text NOT NULL,
    duration_minutes integer NOT NULL,
    speed_mbps numeric(9,2) NULL,
    data_mode text NOT NULL,
    quota_mb integer NULL,
    price numeric(12,2) NOT NULL,
    sort_order integer NOT NULL,
    status text NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid NULL,
    updated_by uuid NULL,
    CONSTRAINT pk_wifi_packages PRIMARY KEY (id),
    CONSTRAINT ck_wifi_packages_name CHECK (char_length(name) BETWEEN 1 AND 48 AND name !~ '[[:cntrl:]]'),
    CONSTRAINT ck_wifi_packages_duration CHECK (duration_minutes BETWEEN 1 AND 43200),
    CONSTRAINT ck_wifi_packages_speed CHECK (speed_mbps IS NULL OR (speed_mbps > 0 AND speed_mbps <= 999999.99)),
    CONSTRAINT ck_wifi_packages_data_mode CHECK (data_mode IN ('Unlimited', 'Limited')),
    CONSTRAINT ck_wifi_packages_quota CHECK (quota_mb IS NULL OR quota_mb BETWEEN 1 AND 1000000),
    CONSTRAINT ck_wifi_packages_quota_mode CHECK ((data_mode = 'Limited') = (quota_mb IS NOT NULL)),
    CONSTRAINT ck_wifi_packages_price CHECK (price >= 0),
    CONSTRAINT ck_wifi_packages_sort_order CHECK (sort_order >= 0),
    CONSTRAINT ck_wifi_packages_status CHECK (status IN ('Active', 'Inactive')),
    CONSTRAINT fk_wifi_packages_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_wifi_packages_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_wifi_packages_created_by ON wifi_packages (created_by);

CREATE INDEX ix_wifi_packages_updated_by ON wifi_packages (updated_by);

CREATE UNIQUE INDEX uq_wifi_packages_active_name ON wifi_packages (lower(name)) WHERE status = 'Active';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261124155117_AddWifiPackages', '10.0.13');

COMMIT;
