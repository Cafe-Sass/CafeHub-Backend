START TRANSACTION;

CREATE TABLE branch_product_availability (
    branch_id uuid NOT NULL,
    product_id uuid NOT NULL,
    is_available boolean NOT NULL,
    reason_code text NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid,
    updated_by uuid,
    CONSTRAINT pk_branch_product_availability PRIMARY KEY (branch_id, product_id),
    CONSTRAINT ck_branch_product_availability_reason_code CHECK (reason_code IN ('manual', 'out_of_stock', 'expired')),
    CONSTRAINT fk_branch_product_availability_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_branch_product_availability_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_branch_product_availability_product_id FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE RESTRICT,
    CONSTRAINT fk_branch_product_availability_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_branch_product_availability_created_by ON branch_product_availability (created_by);

CREATE INDEX ix_branch_product_availability_product_id ON branch_product_availability (product_id);

CREATE INDEX ix_branch_product_availability_updated_by ON branch_product_availability (updated_by);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261122064404_AddBranchProductAvailability', '10.0.13');

COMMIT;
