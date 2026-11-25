START TRANSACTION;

CREATE TABLE printers (
    id uuid NOT NULL,
    branch_id uuid NOT NULL,
    station_id uuid NULL,
    name text NOT NULL,
    profile text NOT NULL,
    connection_kind text NOT NULL,
    status text NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid NULL,
    updated_by uuid NULL,
    CONSTRAINT pk_printers PRIMARY KEY (id),
    CONSTRAINT ck_printers_name CHECK (char_length(name) BETWEEN 1 AND 64 AND name !~ '[[:cntrl:]]'),
    CONSTRAINT ck_printers_profile CHECK (profile IN ('Escpos80mm', 'Escpos58mm')),
    CONSTRAINT ck_printers_connection_kind CHECK (char_length(connection_kind) BETWEEN 1 AND 32 AND connection_kind !~ '[[:cntrl:]]'),
    CONSTRAINT ck_printers_status CHECK (status IN ('Active', 'Inactive')),
    CONSTRAINT fk_printers_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_printers_station_id FOREIGN KEY (station_id) REFERENCES stations (id) ON DELETE RESTRICT,
    CONSTRAINT fk_printers_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_printers_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE TABLE print_routes (
    id uuid NOT NULL,
    branch_id uuid NOT NULL,
    station_id uuid NULL,
    document_type text NOT NULL,
    printer_id uuid NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid NULL,
    updated_by uuid NULL,
    CONSTRAINT pk_print_routes PRIMARY KEY (id),
    CONSTRAINT ck_print_routes_document_type CHECK (document_type IN ('OrderTicket', 'Receipt')),
    CONSTRAINT fk_print_routes_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_print_routes_station_id FOREIGN KEY (station_id) REFERENCES stations (id) ON DELETE RESTRICT,
    CONSTRAINT fk_print_routes_printer_id FOREIGN KEY (printer_id) REFERENCES printers (id) ON DELETE RESTRICT,
    CONSTRAINT fk_print_routes_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_print_routes_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_printers_branch_id ON printers (branch_id);

CREATE INDEX ix_printers_station_id ON printers (station_id);

CREATE INDEX ix_printers_created_by ON printers (created_by);

CREATE INDEX ix_printers_updated_by ON printers (updated_by);

CREATE INDEX ix_print_routes_branch_id ON print_routes (branch_id);

CREATE INDEX ix_print_routes_station_id ON print_routes (station_id);

CREATE INDEX ix_print_routes_printer_id ON print_routes (printer_id);

CREATE INDEX ix_print_routes_created_by ON print_routes (created_by);

CREATE INDEX ix_print_routes_updated_by ON print_routes (updated_by);

CREATE UNIQUE INDEX uq_print_routes_branch_station_document ON print_routes (branch_id, station_id, document_type) NULLS NOT DISTINCT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261124225113_AddPrinters', '10.0.13');

COMMIT;
