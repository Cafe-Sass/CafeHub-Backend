START TRANSACTION;

CREATE TABLE order_response_misses (
    id uuid NOT NULL,
    order_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    expired_at timestamptz NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid,
    updated_by uuid,
    CONSTRAINT pk_order_response_misses PRIMARY KEY (id),
    CONSTRAINT fk_order_response_misses_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_order_response_misses_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_order_response_misses_order_id FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE RESTRICT,
    CONSTRAINT fk_order_response_misses_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE TABLE order_response_miss_holders (
    order_response_miss_id uuid NOT NULL,
    user_id uuid NOT NULL,
    created_at timestamptz NOT NULL,
    CONSTRAINT pk_order_response_miss_holders PRIMARY KEY (order_response_miss_id, user_id),
    CONSTRAINT fk_order_response_miss_holders_miss_id FOREIGN KEY (order_response_miss_id) REFERENCES order_response_misses (id) ON DELETE RESTRICT,
    CONSTRAINT fk_order_response_miss_holders_user_id FOREIGN KEY (user_id) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_order_response_miss_holders_user_id ON order_response_miss_holders (user_id);

CREATE INDEX ix_order_response_misses_branch_expired_at ON order_response_misses (branch_id, expired_at);

CREATE INDEX ix_order_response_misses_created_by ON order_response_misses (created_by);

CREATE INDEX ix_order_response_misses_updated_by ON order_response_misses (updated_by);

CREATE UNIQUE INDEX uq_order_response_misses_order_id ON order_response_misses (order_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261122200337_AddOrderResponseMisses', '10.0.13');

COMMIT;
