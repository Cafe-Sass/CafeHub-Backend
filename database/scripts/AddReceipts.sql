START TRANSACTION;

CREATE TABLE receipt_number_counters (
    branch_id uuid NOT NULL,
    business_date date NOT NULL,
    last_number integer NOT NULL,
    updated_at timestamptz NOT NULL,
    CONSTRAINT pk_receipt_number_counters PRIMARY KEY (branch_id, business_date),
    CONSTRAINT ck_receipt_number_counters_last_number CHECK (last_number >= 1),
    CONSTRAINT fk_receipt_number_counters_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT
);

CREATE TABLE receipts (
    id uuid NOT NULL,
    bill_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    receipt_number text NOT NULL,
    issued_at timestamptz NOT NULL,
    total_amount numeric(12,2) NOT NULL,
    paid_amount numeric(12,2) NOT NULL,
    remaining_amount numeric(12,2) NOT NULL,
    created_at timestamptz NOT NULL,
    created_by uuid NULL,
    CONSTRAINT pk_receipts PRIMARY KEY (id),
    CONSTRAINT ck_receipts_receipt_number_format CHECK (char_length(receipt_number) BETWEEN 11 AND 32 AND receipt_number ~ '^R-[0-9]{8}-[0-9]+$'),
    CONSTRAINT ck_receipts_total_amount_range CHECK (total_amount >= 0),
    CONSTRAINT ck_receipts_paid_amount_range CHECK (paid_amount > 0),
    CONSTRAINT ck_receipts_fully_paid CHECK (remaining_amount = 0),
    CONSTRAINT fk_receipts_bill_id FOREIGN KEY (bill_id) REFERENCES bills (id) ON DELETE RESTRICT,
    CONSTRAINT fk_receipts_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_receipts_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE TABLE receipt_lines (
    id uuid NOT NULL,
    receipt_id uuid NOT NULL,
    order_number integer NOT NULL,
    guest_display_name text NOT NULL,
    product_name text NOT NULL,
    size_name text NOT NULL,
    addons text NULL,
    quantity integer NOT NULL,
    unit_price numeric(12,2) NOT NULL,
    line_total numeric(12,2) NOT NULL,
    created_at timestamptz NOT NULL,
    CONSTRAINT pk_receipt_lines PRIMARY KEY (id),
    CONSTRAINT ck_receipt_lines_order_number CHECK (order_number >= 1),
    CONSTRAINT ck_receipt_lines_guest_name CHECK (char_length(guest_display_name) BETWEEN 1 AND 64 AND guest_display_name !~ '[[:cntrl:]]'),
    CONSTRAINT ck_receipt_lines_product_name CHECK (char_length(product_name) BETWEEN 1 AND 96),
    CONSTRAINT ck_receipt_lines_size_name CHECK (char_length(size_name) BETWEEN 1 AND 32),
    CONSTRAINT ck_receipt_lines_addons CHECK (addons IS NULL OR (char_length(addons) BETWEEN 1 AND 512 AND addons !~ '[[:cntrl:]]')),
    CONSTRAINT ck_receipt_lines_quantity CHECK (quantity BETWEEN 1 AND 50),
    CONSTRAINT ck_receipt_lines_unit_price CHECK (unit_price >= 0 AND unit_price <= 99999999.99),
    CONSTRAINT ck_receipt_lines_line_total CHECK (line_total >= 0 AND line_total <= 99999999.99),
    CONSTRAINT fk_receipt_lines_receipt_id FOREIGN KEY (receipt_id) REFERENCES receipts (id) ON DELETE RESTRICT
);

CREATE TABLE receipt_payments (
    id uuid NOT NULL,
    receipt_id uuid NOT NULL,
    method text NOT NULL,
    amount numeric(12,2) NOT NULL,
    confirmed_at timestamptz NOT NULL,
    created_at timestamptz NOT NULL,
    CONSTRAINT pk_receipt_payments PRIMARY KEY (id),
    CONSTRAINT ck_receipt_payments_method CHECK (method IN ('Cash', 'Online')),
    CONSTRAINT ck_receipt_payments_amount_range CHECK (amount > 0),
    CONSTRAINT fk_receipt_payments_receipt_id FOREIGN KEY (receipt_id) REFERENCES receipts (id) ON DELETE RESTRICT
);

CREATE INDEX ix_receipts_branch_issued_at ON receipts (branch_id, issued_at);

CREATE INDEX ix_receipts_created_by ON receipts (created_by);

CREATE UNIQUE INDEX uq_receipts_bill_id ON receipts (bill_id);

CREATE UNIQUE INDEX uq_receipts_branch_receipt_number ON receipts (branch_id, receipt_number);

CREATE INDEX ix_receipt_lines_receipt_id ON receipt_lines (receipt_id);

CREATE INDEX ix_receipt_payments_receipt_id ON receipt_payments (receipt_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261124110741_AddReceipts', '10.0.13');

COMMIT;
