START TRANSACTION;

CREATE TABLE attendance_records (
    id uuid NOT NULL,
    branch_id uuid NOT NULL,
    staff_id uuid NOT NULL,
    check_in_at timestamptz NOT NULL,
    check_out_at timestamptz NULL,
    check_in_session_id uuid NOT NULL,
    check_out_session_id uuid NULL,
    source text NOT NULL DEFAULT 'Qr',
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    CONSTRAINT pk_attendance_records PRIMARY KEY (id),
    CONSTRAINT ck_attendance_records_source CHECK (source IN ('Qr', 'Device')),
    CONSTRAINT ck_attendance_records_window CHECK (check_out_at IS NULL OR check_out_at > check_in_at),
    CONSTRAINT fk_attendance_records_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_attendance_records_staff_id FOREIGN KEY (staff_id) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_attendance_records_check_in_session_id FOREIGN KEY (check_in_session_id) REFERENCES attendance_qr_sessions (id) ON DELETE RESTRICT,
    CONSTRAINT fk_attendance_records_check_out_session_id FOREIGN KEY (check_out_session_id) REFERENCES attendance_qr_sessions (id) ON DELETE RESTRICT
);

CREATE INDEX ix_attendance_records_branch_check_in_at ON attendance_records (branch_id, check_in_at);

CREATE INDEX ix_attendance_records_check_in_session_id ON attendance_records (check_in_session_id);

CREATE INDEX ix_attendance_records_check_out_session_id ON attendance_records (check_out_session_id);

CREATE INDEX ix_attendance_records_staff_check_in_at ON attendance_records (staff_id, check_in_at);

CREATE UNIQUE INDEX uq_attendance_records_open_staff ON attendance_records (staff_id) WHERE check_out_at IS NULL;

CREATE UNIQUE INDEX uq_attendance_records_staff_check_in_session ON attendance_records (staff_id, check_in_session_id);

CREATE UNIQUE INDEX uq_attendance_records_staff_check_out_session ON attendance_records (staff_id, check_out_session_id) WHERE check_out_session_id IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261126150224_AddAttendanceRecords', '10.0.13');

COMMIT;
