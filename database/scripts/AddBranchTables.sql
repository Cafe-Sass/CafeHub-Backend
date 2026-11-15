START TRANSACTION;
CREATE TABLE branch_tables (
    id uuid NOT NULL,
    branch_id uuid NOT NULL,
    number integer NOT NULL,
    label text NULL,
    qr_code text NOT NULL,
    status text NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid NULL,
    updated_by uuid NULL,
    CONSTRAINT pk_branch_tables PRIMARY KEY (id),
    CONSTRAINT ck_branch_tables_number_range CHECK (number >= 1 AND number <= 9999),
    CONSTRAINT ck_branch_tables_label_length CHECK (label IS NULL OR (length(label) >= 1 AND length(label) <= 64)),
    CONSTRAINT ck_branch_tables_qr_code_format CHECK (length(qr_code) = 22 AND qr_code ~ '^[A-Za-z0-9_-]{22}$'),
    CONSTRAINT ck_branch_tables_status CHECK (status IN ('Active', 'Inactive')),
    CONSTRAINT fk_branch_tables_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_branch_tables_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_branch_tables_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_branch_tables_branch_id ON branch_tables (branch_id);

CREATE INDEX ix_branch_tables_created_by ON branch_tables (created_by);

CREATE INDEX ix_branch_tables_updated_by ON branch_tables (updated_by);

CREATE UNIQUE INDEX uq_branch_tables_qr_code ON branch_tables (qr_code);

CREATE UNIQUE INDEX uq_branch_tables_branch_active_number ON branch_tables (branch_id, number) WHERE status = 'Active';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261115190000_AddBranchTables', '10.0.13');

COMMIT;
