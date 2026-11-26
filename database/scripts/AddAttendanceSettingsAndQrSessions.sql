START TRANSACTION;

CREATE TABLE attendance_qr_sessions (
    id uuid NOT NULL,
    branch_id uuid NOT NULL,
    token text NOT NULL,
    valid_from timestamptz NOT NULL,
    valid_to timestamptz NOT NULL,
    created_at timestamptz NOT NULL,
    created_by uuid NOT NULL,
    CONSTRAINT pk_attendance_qr_sessions PRIMARY KEY (id),
    CONSTRAINT ck_attendance_qr_sessions_token CHECK (token ~ '^[A-Za-z0-9_-]{43}$'),
    CONSTRAINT ck_attendance_qr_sessions_window CHECK (valid_to > valid_from),
    CONSTRAINT fk_attendance_qr_sessions_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_attendance_qr_sessions_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_attendance_qr_sessions_branch_valid_to ON attendance_qr_sessions (branch_id, valid_to);

CREATE INDEX ix_attendance_qr_sessions_created_by ON attendance_qr_sessions (created_by);

CREATE UNIQUE INDEX uq_attendance_qr_sessions_token ON attendance_qr_sessions (token);

CREATE TABLE attendance_settings (
    id uuid NOT NULL,
    mode text NOT NULL DEFAULT 'Off',
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid NULL,
    updated_by uuid NULL,
    CONSTRAINT pk_attendance_settings PRIMARY KEY (id),
    CONSTRAINT ck_attendance_settings_mode CHECK (mode IN ('Off', 'Qr', 'Device')),
    CONSTRAINT fk_attendance_settings_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_attendance_settings_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_attendance_settings_created_by ON attendance_settings (created_by);

CREATE INDEX ix_attendance_settings_updated_by ON attendance_settings (updated_by);

CREATE UNIQUE INDEX uq_attendance_settings_singleton ON attendance_settings ((true));

INSERT INTO attendance_settings (id, mode, created_at, updated_at, created_by, updated_by)
VALUES ('7c1d3f5a-2b4e-4d8a-9c6f-0e5b8a2d1f47', 'Off', now(), now(), NULL, NULL);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261126140322_AddAttendanceSettingsAndQrSessions', '10.0.13');

COMMIT;
