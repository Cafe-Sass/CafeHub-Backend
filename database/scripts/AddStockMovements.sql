START TRANSACTION;

CREATE TABLE stock_movements (
    id uuid NOT NULL,
    item_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    batch_id uuid NOT NULL,
    type text NOT NULL,
    quantity numeric(12,3) NOT NULL,
    reason text NULL,
    idempotency_key text NOT NULL,
    created_at timestamptz NOT NULL,
    created_by uuid NULL,
    CONSTRAINT pk_stock_movements PRIMARY KEY (id),
    CONSTRAINT ck_stock_movements_type CHECK (type IN ('Receive', 'Consume', 'Waste', 'Adjust')),
    CONSTRAINT ck_stock_movements_quantity CHECK (quantity <> 0 AND quantity >= -999999999.999 AND quantity <= 999999999.999),
    CONSTRAINT ck_stock_movements_quantity_sign CHECK (((type = 'Receive' AND quantity > 0) OR (type IN ('Consume', 'Waste') AND quantity < 0) OR (type = 'Adjust' AND quantity <> 0))),
    CONSTRAINT ck_stock_movements_reason CHECK (reason IS NULL OR (char_length(btrim(reason)) BETWEEN 1 AND 500 AND reason !~ '[[:cntrl:]]')),
    CONSTRAINT ck_stock_movements_reason_required CHECK (type = 'Receive' OR reason IS NOT NULL),
    CONSTRAINT ck_stock_movements_idempotency_key_length CHECK (char_length(idempotency_key) BETWEEN 8 AND 64),
    CONSTRAINT fk_stock_movements_item_id FOREIGN KEY (item_id) REFERENCES inventory_items (id) ON DELETE RESTRICT,
    CONSTRAINT fk_stock_movements_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_stock_movements_batch_id FOREIGN KEY (batch_id) REFERENCES inventory_batches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_stock_movements_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_stock_movements_item_id ON stock_movements (item_id);

CREATE INDEX ix_stock_movements_branch_id ON stock_movements (branch_id);

CREATE INDEX ix_stock_movements_batch_id ON stock_movements (batch_id);

CREATE INDEX ix_stock_movements_created_by ON stock_movements (created_by);

CREATE UNIQUE INDEX uq_stock_movements_branch_key_batch
ON stock_movements (branch_id, idempotency_key, batch_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261125181832_AddStockMovements', '10.0.13');

COMMIT;
