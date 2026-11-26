START TRANSACTION;

CREATE TABLE attendance_devices (
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
    CONSTRAINT pk_attendance_devices PRIMARY KEY (id),
    CONSTRAINT ck_attendance_devices_name CHECK (char_length(name) BETWEEN 1 AND 64 AND name !~ '[[:cntrl:]]'),
    CONSTRAINT ck_attendance_devices_key_hash CHECK (char_length(key_hash) = 64),
    CONSTRAINT ck_attendance_devices_status CHECK (status IN ('Active', 'Revoked')),
    CONSTRAINT fk_attendance_devices_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_attendance_devices_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_attendance_devices_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_attendance_devices_branch_id ON attendance_devices (branch_id);

CREATE INDEX ix_attendance_devices_created_by ON attendance_devices (created_by);

CREATE INDEX ix_attendance_devices_updated_by ON attendance_devices (updated_by);

CREATE UNIQUE INDEX uq_attendance_devices_key_hash ON attendance_devices (key_hash);

CREATE TABLE attendance_device_codes (
    id uuid NOT NULL,
    branch_id uuid NOT NULL,
    staff_id uuid NOT NULL,
    device_code text NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid NULL,
    updated_by uuid NULL,
    CONSTRAINT pk_attendance_device_codes PRIMARY KEY (id),
    CONSTRAINT ck_attendance_device_codes_value CHECK (char_length(device_code) BETWEEN 1 AND 64 AND device_code !~ '[[:cntrl:]]'),
    CONSTRAINT fk_attendance_device_codes_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_attendance_device_codes_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_attendance_device_codes_staff_id FOREIGN KEY (staff_id) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_attendance_device_codes_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_attendance_device_codes_branch_id ON attendance_device_codes (branch_id);

CREATE INDEX ix_attendance_device_codes_created_by ON attendance_device_codes (created_by);

CREATE INDEX ix_attendance_device_codes_staff_id ON attendance_device_codes (staff_id);

CREATE INDEX ix_attendance_device_codes_updated_by ON attendance_device_codes (updated_by);

CREATE UNIQUE INDEX uq_attendance_device_codes_branch_code ON attendance_device_codes (branch_id, device_code);

CREATE UNIQUE INDEX uq_attendance_device_codes_branch_staff ON attendance_device_codes (branch_id, staff_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261126165108_AddAttendanceDevices', '10.0.13');

COMMIT;
