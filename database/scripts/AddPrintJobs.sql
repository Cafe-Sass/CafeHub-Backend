START TRANSACTION;

CREATE TABLE print_jobs (
    id uuid NOT NULL,
    branch_id uuid NOT NULL,
    printer_id uuid NOT NULL,
    document_type text NOT NULL,
    source_type text NOT NULL,
    source_id uuid NOT NULL,
    dedupe_key text NOT NULL,
    status text NOT NULL,
    attempts integer NOT NULL,
    max_attempts integer NOT NULL,
    last_error_code text NULL,
    document_payload jsonb NOT NULL,
    lease_expires_at timestamptz NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    CONSTRAINT pk_print_jobs PRIMARY KEY (id),
    CONSTRAINT ck_print_jobs_document_type CHECK (document_type IN ('OrderTicket', 'Receipt')),
    CONSTRAINT ck_print_jobs_source_type CHECK (source_type IN ('Order', 'Bill')),
    CONSTRAINT ck_print_jobs_dedupe_key_length CHECK (char_length(dedupe_key) BETWEEN 1 AND 160),
    CONSTRAINT ck_print_jobs_status CHECK (status IN ('Queued', 'Claimed', 'Succeeded', 'Failed', 'Cancelled')),
    CONSTRAINT ck_print_jobs_attempts CHECK (attempts >= 0),
    CONSTRAINT ck_print_jobs_max_attempts CHECK (max_attempts BETWEEN 1 AND 3),
    CONSTRAINT ck_print_jobs_last_error_code CHECK (last_error_code IS NULL OR last_error_code IN ('PrinterOffline', 'PaperOut', 'PayloadRejected', 'Unknown')),
    CONSTRAINT fk_print_jobs_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_print_jobs_printer_id FOREIGN KEY (printer_id) REFERENCES printers (id) ON DELETE RESTRICT
);

CREATE INDEX ix_print_jobs_branch_id ON print_jobs (branch_id);

CREATE INDEX ix_print_jobs_printer_id ON print_jobs (printer_id);

CREATE UNIQUE INDEX uq_print_jobs_branch_dedupe_key ON print_jobs (branch_id, dedupe_key);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261125081313_AddPrintJobs', '10.0.13');

COMMIT;
