START TRANSACTION;

CREATE TABLE print_connectors (
    id uuid NOT NULL,
    branch_id uuid NOT NULL,
    name text NOT NULL,
    key_hash text NOT NULL,
    status text NOT NULL,
    last_seen_at timestamptz NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid NULL,
    updated_by uuid NULL,
    CONSTRAINT pk_print_connectors PRIMARY KEY (id),
    CONSTRAINT ck_print_connectors_name CHECK (char_length(name) BETWEEN 1 AND 64 AND name !~ '[[:cntrl:]]'),
    CONSTRAINT ck_print_connectors_key_hash CHECK (char_length(key_hash) = 64),
    CONSTRAINT ck_print_connectors_status CHECK (status IN ('Active', 'Revoked')),
    CONSTRAINT fk_print_connectors_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_print_connectors_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_print_connectors_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_print_connectors_branch_id ON print_connectors (branch_id);

CREATE INDEX ix_print_connectors_created_by ON print_connectors (created_by);

CREATE INDEX ix_print_connectors_updated_by ON print_connectors (updated_by);

CREATE UNIQUE INDEX uq_print_connectors_key_hash ON print_connectors (key_hash);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261125110824_AddPrintConnectors', '10.0.13');

COMMIT;
