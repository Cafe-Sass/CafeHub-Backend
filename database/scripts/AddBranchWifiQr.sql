START TRANSACTION;

CREATE TABLE branch_wifi_qr (
    branch_id uuid NOT NULL,
    qr_code text NOT NULL,
    status text NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid NULL,
    updated_by uuid NULL,
    CONSTRAINT pk_branch_wifi_qr PRIMARY KEY (branch_id),
    CONSTRAINT ck_branch_wifi_qr_code_format CHECK (char_length(qr_code) = 22 AND qr_code ~ '^[A-Za-z0-9_-]{22}$'),
    CONSTRAINT ck_branch_wifi_qr_status CHECK (status IN ('Active', 'Inactive')),
    CONSTRAINT fk_branch_wifi_qr_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_branch_wifi_qr_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_branch_wifi_qr_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_branch_wifi_qr_created_by ON branch_wifi_qr (created_by);

CREATE INDEX ix_branch_wifi_qr_updated_by ON branch_wifi_qr (updated_by);

CREATE UNIQUE INDEX uq_branch_wifi_qr_code ON branch_wifi_qr (qr_code);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261124172402_AddBranchWifiQr', '10.0.13');

COMMIT;
