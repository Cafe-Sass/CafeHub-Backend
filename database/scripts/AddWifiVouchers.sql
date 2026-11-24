START TRANSACTION;

CREATE TABLE wifi_vouchers (
    id uuid NOT NULL,
    package_id uuid NOT NULL,
    code text NOT NULL,
    status text NOT NULL,
    assigned_order_item_id uuid NULL,
    assigned_at timestamptz NULL,
    imported_by uuid NULL,
    imported_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    CONSTRAINT pk_wifi_vouchers PRIMARY KEY (id),
    CONSTRAINT ck_wifi_vouchers_code CHECK (char_length(code) BETWEEN 4 AND 64 AND code ~ '^[A-Za-z0-9-]+$'),
    CONSTRAINT ck_wifi_vouchers_status CHECK (status IN ('Available', 'Assigned', 'Void')),
    CONSTRAINT ck_wifi_vouchers_assignment_pair CHECK ((status = 'Assigned') = (assigned_order_item_id IS NOT NULL AND assigned_at IS NOT NULL)),
    CONSTRAINT fk_wifi_vouchers_package_id FOREIGN KEY (package_id) REFERENCES wifi_packages (id) ON DELETE RESTRICT,
    CONSTRAINT fk_wifi_vouchers_assigned_order_item_id FOREIGN KEY (assigned_order_item_id) REFERENCES order_items (id) ON DELETE RESTRICT,
    CONSTRAINT fk_wifi_vouchers_imported_by FOREIGN KEY (imported_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_wifi_vouchers_imported_by ON wifi_vouchers (imported_by);

CREATE UNIQUE INDEX uq_wifi_vouchers_package_id_code ON wifi_vouchers (package_id, code);

CREATE UNIQUE INDEX uq_wifi_vouchers_assigned_order_item_id ON wifi_vouchers (assigned_order_item_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261124183133_AddWifiVouchers', '10.0.13');

COMMIT;
