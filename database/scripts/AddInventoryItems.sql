START TRANSACTION;

CREATE TABLE inventory_items (
    id uuid NOT NULL,
    name text NOT NULL,
    unit text NOT NULL,
    low_stock_threshold numeric(12,3) NOT NULL,
    tracks_expiry boolean NOT NULL,
    near_expiry_days integer NOT NULL DEFAULT 7,
    status text NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid NULL,
    updated_by uuid NULL,
    CONSTRAINT pk_inventory_items PRIMARY KEY (id),
    CONSTRAINT ck_inventory_items_name CHECK (char_length(name) BETWEEN 1 AND 96 AND name !~ '[[:cntrl:]]'),
    CONSTRAINT ck_inventory_items_unit CHECK (unit IN ('Kg', 'G', 'L', 'Ml', 'Piece', 'Pack')),
    CONSTRAINT ck_inventory_items_low_stock_threshold CHECK (low_stock_threshold >= 0),
    CONSTRAINT ck_inventory_items_near_expiry_days CHECK (near_expiry_days >= 0),
    CONSTRAINT ck_inventory_items_status CHECK (status IN ('Active', 'Inactive')),
    CONSTRAINT fk_inventory_items_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_inventory_items_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_inventory_items_created_by ON inventory_items (created_by);

CREATE INDEX ix_inventory_items_updated_by ON inventory_items (updated_by);

CREATE UNIQUE INDEX uq_inventory_items_active_name
ON inventory_items (lower(name))
WHERE status = 'Active';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261125125659_AddInventoryItems', '10.0.13');

COMMIT;
