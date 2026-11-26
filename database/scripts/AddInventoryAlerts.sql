START TRANSACTION;

CREATE TABLE inventory_alerts (
    id uuid NOT NULL,
    item_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    batch_id uuid NULL,
    kind text NOT NULL,
    state text NOT NULL,
    raised_at timestamptz NOT NULL,
    resolved_at timestamptz NULL,
    notified_at timestamptz NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid NULL,
    updated_by uuid NULL,
    CONSTRAINT pk_inventory_alerts PRIMARY KEY (id),
    CONSTRAINT ck_inventory_alerts_kind CHECK (kind IN ('LowStock', 'NearExpiry', 'Expired')),
    CONSTRAINT ck_inventory_alerts_state CHECK (state IN ('Open', 'Resolved')),
    CONSTRAINT ck_inventory_alerts_batch CHECK (((kind = 'LowStock' AND batch_id IS NULL) OR (kind IN ('NearExpiry', 'Expired') AND batch_id IS NOT NULL))),
    CONSTRAINT ck_inventory_alerts_resolved CHECK (((state = 'Open' AND resolved_at IS NULL) OR (state = 'Resolved' AND resolved_at IS NOT NULL))),
    CONSTRAINT ck_inventory_alerts_resolved_not_before_raised CHECK (resolved_at IS NULL OR resolved_at >= raised_at),
    CONSTRAINT ck_inventory_alerts_notified_not_before_raised CHECK (notified_at IS NULL OR notified_at >= raised_at),
    CONSTRAINT fk_inventory_alerts_item_id FOREIGN KEY (item_id) REFERENCES inventory_items (id) ON DELETE RESTRICT,
    CONSTRAINT fk_inventory_alerts_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_inventory_alerts_batch_id FOREIGN KEY (batch_id) REFERENCES inventory_batches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_inventory_alerts_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_inventory_alerts_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_inventory_alerts_item_id ON inventory_alerts (item_id);

CREATE INDEX ix_inventory_alerts_branch_id ON inventory_alerts (branch_id);

CREATE INDEX ix_inventory_alerts_batch_id ON inventory_alerts (batch_id);

CREATE INDEX ix_inventory_alerts_created_by ON inventory_alerts (created_by);

CREATE INDEX ix_inventory_alerts_updated_by ON inventory_alerts (updated_by);

CREATE UNIQUE INDEX uq_inventory_alerts_open_batch
ON inventory_alerts (item_id, branch_id, kind, batch_id)
WHERE state = 'Open' AND batch_id IS NOT NULL;

CREATE UNIQUE INDEX uq_inventory_alerts_open_item
ON inventory_alerts (item_id, branch_id, kind)
WHERE state = 'Open' AND batch_id IS NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261126131109_AddInventoryAlerts', '10.0.13');

COMMIT;
