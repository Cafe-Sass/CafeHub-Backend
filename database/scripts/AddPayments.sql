START TRANSACTION;

CREATE TABLE payments (
    id uuid NOT NULL,
    bill_id uuid NOT NULL,
    order_id uuid NULL,
    method text NOT NULL,
    status text NOT NULL,
    amount numeric(12,2) NOT NULL,
    confirmed_by_user_id uuid NULL,
    confirmed_at timestamptz NULL,
    idempotency_key text NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid,
    updated_by uuid,
    CONSTRAINT pk_payments PRIMARY KEY (id),
    CONSTRAINT ck_payments_amount_range CHECK (amount > 0),
    CONSTRAINT ck_payments_confirmation_pair CHECK ((status = 'Confirmed') = (confirmed_at IS NOT NULL AND confirmed_by_user_id IS NOT NULL)),
    CONSTRAINT ck_payments_idempotency_key_length CHECK (char_length(idempotency_key) BETWEEN 1 AND 128),
    CONSTRAINT ck_payments_method CHECK (method IN ('Cash', 'Online')),
    CONSTRAINT ck_payments_status CHECK (status IN ('Confirmed', 'PendingReview', 'Rejected')),
    CONSTRAINT fk_payments_bill_id FOREIGN KEY (bill_id) REFERENCES bills (id) ON DELETE RESTRICT,
    CONSTRAINT fk_payments_confirmed_by_user_id FOREIGN KEY (confirmed_by_user_id) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_payments_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_payments_order_id FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE RESTRICT,
    CONSTRAINT fk_payments_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_payments_confirmed_by_user_id ON payments (confirmed_by_user_id);

CREATE INDEX ix_payments_created_by ON payments (created_by);

CREATE INDEX ix_payments_order_id ON payments (order_id);

CREATE INDEX ix_payments_updated_by ON payments (updated_by);

CREATE UNIQUE INDEX uq_payments_bill_idempotency_key ON payments (bill_id, idempotency_key);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261123163209_AddPayments', '10.0.13');

COMMIT;
