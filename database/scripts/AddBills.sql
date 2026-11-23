START TRANSACTION;

CREATE TABLE bills (
    id uuid NOT NULL,
    branch_id uuid NOT NULL,
    table_id uuid NOT NULL,
    status text NOT NULL,
    public_reference text NOT NULL,
    opened_at timestamptz NOT NULL,
    closed_at timestamptz NULL,
    closed_by_user_id uuid NULL,
    close_reason text NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid,
    updated_by uuid,
    CONSTRAINT pk_bills PRIMARY KEY (id),
    CONSTRAINT ck_bills_status CHECK (status IN ('Open', 'Closed')),
    CONSTRAINT ck_bills_public_reference_format CHECK (char_length(public_reference) = 22 AND public_reference ~ '^[A-Za-z0-9_-]{22}$'),
    CONSTRAINT ck_bills_close_pair CHECK ((closed_at IS NULL AND closed_by_user_id IS NULL AND close_reason IS NULL) OR (closed_at IS NOT NULL AND closed_by_user_id IS NOT NULL AND close_reason IS NOT NULL)),
    CONSTRAINT ck_bills_close_reason CHECK (close_reason IS NULL OR (status = 'Closed' AND char_length(close_reason) BETWEEN 3 AND 256)),
    CONSTRAINT fk_bills_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_bills_closed_by_user_id FOREIGN KEY (closed_by_user_id) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_bills_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_bills_table_id FOREIGN KEY (table_id) REFERENCES branch_tables (id) ON DELETE RESTRICT,
    CONSTRAINT fk_bills_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

ALTER TABLE orders ADD COLUMN bill_id uuid NULL;

CREATE INDEX ix_bills_branch_id ON bills (branch_id);

CREATE INDEX ix_bills_closed_by_user_id ON bills (closed_by_user_id);

CREATE INDEX ix_bills_created_by ON bills (created_by);

CREATE INDEX ix_bills_table_id ON bills (table_id);

CREATE INDEX ix_bills_updated_by ON bills (updated_by);

CREATE INDEX ix_orders_bill_id ON orders (bill_id);

CREATE UNIQUE INDEX uq_bills_public_reference ON bills (public_reference);

CREATE UNIQUE INDEX uq_bills_table_open ON bills (table_id) WHERE status = 'Open';

ALTER TABLE orders ADD CONSTRAINT fk_orders_bill_id FOREIGN KEY (bill_id) REFERENCES bills (id) ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261123155043_AddBills', '10.0.13');

COMMIT;
