START TRANSACTION;

CREATE TABLE order_number_counters (
    branch_id uuid NOT NULL,
    business_date date NOT NULL,
    last_number integer NOT NULL,
    updated_at timestamptz NOT NULL,
    CONSTRAINT pk_order_number_counters PRIMARY KEY (branch_id, business_date),
    CONSTRAINT ck_order_number_counters_last_number CHECK (last_number >= 1),
    CONSTRAINT fk_order_number_counters_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT
);

CREATE TABLE orders (
    id uuid NOT NULL,
    branch_id uuid NOT NULL,
    table_id uuid NOT NULL,
    order_number integer NOT NULL,
    business_date date NOT NULL,
    service_type text NOT NULL,
    status text NOT NULL,
    source text NOT NULL,
    guest_display_name text NOT NULL,
    guest_device_id text,
    placed_by_user_id uuid,
    public_reference text NOT NULL,
    idempotency_key text NOT NULL,
    request_hash text,
    total_amount numeric(12,2) NOT NULL,
    submitted_at timestamptz NOT NULL,
    response_deadline timestamptz NOT NULL,
    responded_by_user_id uuid,
    responded_at timestamptz,
    rejection_reason text,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid,
    updated_by uuid,
    CONSTRAINT pk_orders PRIMARY KEY (id),
    CONSTRAINT ck_orders_decision_pair CHECK ((responded_at IS NULL AND responded_by_user_id IS NULL AND rejection_reason IS NULL) OR (responded_at IS NOT NULL AND responded_by_user_id IS NOT NULL)),
    CONSTRAINT ck_orders_guest_device_id_format CHECK (guest_device_id IS NULL OR (char_length(guest_device_id) BETWEEN 16 AND 64 AND guest_device_id ~ '^[A-Za-z0-9_-]+$')),
    CONSTRAINT ck_orders_guest_display_name_length CHECK (char_length(guest_display_name) BETWEEN 1 AND 64),
    CONSTRAINT ck_orders_idempotency_key_length CHECK (char_length(idempotency_key) BETWEEN 1 AND 128),
    CONSTRAINT ck_orders_order_number CHECK (order_number >= 1),
    CONSTRAINT ck_orders_public_reference_format CHECK (char_length(public_reference) = 22 AND public_reference ~ '^[A-Za-z0-9_-]{22}$'),
    CONSTRAINT ck_orders_rejection_reason CHECK (rejection_reason IS NULL OR (status = 'Rejected' AND char_length(rejection_reason) BETWEEN 1 AND 256)),
    CONSTRAINT ck_orders_request_hash_format CHECK (request_hash IS NULL OR request_hash ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_orders_response_deadline CHECK (response_deadline > submitted_at),
    CONSTRAINT ck_orders_service_type CHECK (service_type IN ('DineIn', 'Takeaway')),
    CONSTRAINT ck_orders_source CHECK (source IN ('Guest', 'Captain')),
    CONSTRAINT ck_orders_status CHECK (status IN ('Pending', 'Accepted', 'Rejected', 'Expired')),
    CONSTRAINT ck_orders_total_amount_range CHECK (total_amount >= 0),
    CONSTRAINT fk_orders_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_orders_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_orders_placed_by_user_id FOREIGN KEY (placed_by_user_id) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_orders_responded_by_user_id FOREIGN KEY (responded_by_user_id) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_orders_table_id FOREIGN KEY (table_id) REFERENCES branch_tables (id) ON DELETE RESTRICT,
    CONSTRAINT fk_orders_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE TABLE order_items (
    id uuid NOT NULL,
    order_id uuid NOT NULL,
    line_number integer NOT NULL,
    product_id uuid,
    product_size_id uuid,
    station_id uuid,
    product_name text NOT NULL,
    size_name text NOT NULL,
    unit_price numeric(12,2) NOT NULL,
    quantity integer NOT NULL,
    line_total numeric(12,2) NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid,
    updated_by uuid,
    CONSTRAINT pk_order_items PRIMARY KEY (id),
    CONSTRAINT ck_order_items_line_number CHECK (line_number >= 1),
    CONSTRAINT ck_order_items_line_total_range CHECK (line_total >= 0 AND line_total <= 99999999.99),
    CONSTRAINT ck_order_items_product_name_length CHECK (char_length(product_name) BETWEEN 1 AND 96),
    CONSTRAINT ck_order_items_quantity_range CHECK (quantity BETWEEN 1 AND 50),
    CONSTRAINT ck_order_items_size_name_length CHECK (char_length(size_name) BETWEEN 1 AND 32),
    CONSTRAINT ck_order_items_unit_price_range CHECK (unit_price >= 0 AND unit_price <= 99999999.99),
    CONSTRAINT fk_order_items_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_order_items_order_id FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE RESTRICT,
    CONSTRAINT fk_order_items_product_id FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE RESTRICT,
    CONSTRAINT fk_order_items_product_size_id FOREIGN KEY (product_size_id) REFERENCES product_sizes (id) ON DELETE RESTRICT,
    CONSTRAINT fk_order_items_station_id FOREIGN KEY (station_id) REFERENCES stations (id) ON DELETE RESTRICT,
    CONSTRAINT fk_order_items_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE TABLE order_item_addons (
    id uuid NOT NULL,
    order_item_id uuid NOT NULL,
    product_addon_id uuid,
    addon_name text NOT NULL,
    price numeric(12,2) NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid,
    updated_by uuid,
    CONSTRAINT pk_order_item_addons PRIMARY KEY (id),
    CONSTRAINT ck_order_item_addons_name_length CHECK (char_length(addon_name) BETWEEN 1 AND 48),
    CONSTRAINT ck_order_item_addons_price_range CHECK (price >= 0 AND price <= 99999999.99),
    CONSTRAINT fk_order_item_addons_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_order_item_addons_order_item_id FOREIGN KEY (order_item_id) REFERENCES order_items (id) ON DELETE RESTRICT,
    CONSTRAINT fk_order_item_addons_product_addon_id FOREIGN KEY (product_addon_id) REFERENCES product_addons (id) ON DELETE RESTRICT,
    CONSTRAINT fk_order_item_addons_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_order_item_addons_created_by ON order_item_addons (created_by);

CREATE INDEX ix_order_item_addons_order_item_id ON order_item_addons (order_item_id);

CREATE INDEX ix_order_item_addons_product_addon_id ON order_item_addons (product_addon_id);

CREATE INDEX ix_order_item_addons_updated_by ON order_item_addons (updated_by);

CREATE INDEX ix_order_items_created_by ON order_items (created_by);

CREATE INDEX ix_order_items_order_id ON order_items (order_id);

CREATE INDEX ix_order_items_product_id ON order_items (product_id);

CREATE INDEX ix_order_items_product_size_id ON order_items (product_size_id);

CREATE INDEX ix_order_items_station_id ON order_items (station_id);

CREATE INDEX ix_order_items_updated_by ON order_items (updated_by);

CREATE UNIQUE INDEX uq_order_items_order_id_line_number ON order_items (order_id, line_number);

CREATE INDEX ix_orders_branch_id_status_submitted_at ON orders (branch_id, status, submitted_at);

CREATE INDEX ix_orders_created_by ON orders (created_by);

CREATE INDEX ix_orders_placed_by_user_id ON orders (placed_by_user_id);

CREATE INDEX ix_orders_responded_by_user_id ON orders (responded_by_user_id);

CREATE INDEX ix_orders_table_id ON orders (table_id);

CREATE INDEX ix_orders_updated_by ON orders (updated_by);

CREATE UNIQUE INDEX uq_orders_branch_business_date_number ON orders (branch_id, business_date, order_number);

CREATE UNIQUE INDEX uq_orders_branch_idempotency_key ON orders (branch_id, idempotency_key);

CREATE UNIQUE INDEX uq_orders_public_reference ON orders (public_reference);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261122093328_AddOrders', '10.0.13');

COMMIT;
