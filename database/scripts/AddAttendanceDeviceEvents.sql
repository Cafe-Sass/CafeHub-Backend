START TRANSACTION;

ALTER TABLE attendance_records ALTER COLUMN check_in_session_id DROP NOT NULL;

ALTER TABLE attendance_records ADD CONSTRAINT ck_attendance_records_source_shape CHECK ((source = 'Qr' AND check_in_session_id IS NOT NULL) OR (source = 'Device' AND check_in_session_id IS NULL AND check_out_session_id IS NULL));

CREATE TABLE attendance_device_events (
    id uuid NOT NULL,
    device_id uuid NOT NULL,
    device_event_id text NOT NULL,
    device_code text NOT NULL,
    occurred_at timestamptz NOT NULL,
    direction text NULL,
    status text NOT NULL,
    record_id uuid NULL,
    received_at timestamptz NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    CONSTRAINT pk_attendance_device_events PRIMARY KEY (id),
    CONSTRAINT ck_attendance_device_events_event_id CHECK (char_length(device_event_id) BETWEEN 1 AND 128 AND device_event_id !~ '[[:cntrl:]]'),
    CONSTRAINT ck_attendance_device_events_code CHECK (char_length(device_code) BETWEEN 1 AND 64 AND device_code !~ '[[:cntrl:]]'),
    CONSTRAINT ck_attendance_device_events_direction CHECK (direction IS NULL OR direction IN ('In', 'Out')),
    CONSTRAINT ck_attendance_device_events_status CHECK (status IN ('Processed', 'Unmatched')),
    CONSTRAINT fk_attendance_device_events_device_id FOREIGN KEY (device_id) REFERENCES attendance_devices (id) ON DELETE RESTRICT,
    CONSTRAINT fk_attendance_device_events_record_id FOREIGN KEY (record_id) REFERENCES attendance_records (id) ON DELETE RESTRICT
);

CREATE INDEX ix_attendance_device_events_device_status_occurred_at ON attendance_device_events (device_id, status, occurred_at);

CREATE INDEX ix_attendance_device_events_record_id ON attendance_device_events (record_id);

CREATE UNIQUE INDEX uq_attendance_device_events_device_event ON attendance_device_events (device_id, device_event_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261126181654_AddAttendanceDeviceEvents', '10.0.13');

COMMIT;
