START TRANSACTION;

CREATE TABLE inventory_batches (
    id uuid NOT NULL,
    item_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    received_quantity numeric(12,3) NOT NULL,
    remaining_quantity numeric(12,3) NOT NULL,
    received_date date NOT NULL,
    expiry_date date NULL,
    received_by_user_id uuid NOT NULL,
    idempotency_key text NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid NULL,
    updated_by uuid NULL,
    CONSTRAINT pk_inventory_batches PRIMARY KEY (id),
    CONSTRAINT ck_inventory_batches_received_quantity CHECK (received_quantity >= 0),
    CONSTRAINT ck_inventory_batches_remaining_quantity CHECK (remaining_quantity >= 0 AND remaining_quantity <= received_quantity),
    CONSTRAINT ck_inventory_batches_expiry_not_before_received CHECK (expiry_date IS NULL OR expiry_date >= received_date),
    CONSTRAINT ck_inventory_batches_idempotency_key_length CHECK (char_length(idempotency_key) BETWEEN 8 AND 64),
    CONSTRAINT fk_inventory_batches_item_id FOREIGN KEY (item_id) REFERENCES inventory_items (id) ON DELETE RESTRICT,
    CONSTRAINT fk_inventory_batches_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_inventory_batches_received_by_user_id FOREIGN KEY (received_by_user_id) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_inventory_batches_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_inventory_batches_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_inventory_batches_item_id ON inventory_batches (item_id);

CREATE INDEX ix_inventory_batches_received_by_user_id ON inventory_batches (received_by_user_id);

CREATE INDEX ix_inventory_batches_created_by ON inventory_batches (created_by);

CREATE INDEX ix_inventory_batches_updated_by ON inventory_batches (updated_by);

CREATE UNIQUE INDEX uq_inventory_batches_branch_idempotency_key
ON inventory_batches (branch_id, idempotency_key);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261125134604_AddInventoryBatches', '10.0.13');

COMMIT;
