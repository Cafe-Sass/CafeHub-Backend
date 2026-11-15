START TRANSACTION;
CREATE TABLE stations (
    id uuid NOT NULL,
    branch_id uuid NOT NULL,
    name text NOT NULL,
    status text NOT NULL,
    sort_order integer NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid NULL,
    updated_by uuid NULL,
    CONSTRAINT pk_stations PRIMARY KEY (id),
    CONSTRAINT ck_stations_name_length CHECK (length(name) >= 1 AND length(name) <= 64),
    CONSTRAINT ck_stations_status CHECK (status IN ('Active', 'Inactive')),
    CONSTRAINT ck_stations_sort_order CHECK (sort_order >= 0),
    CONSTRAINT fk_stations_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_stations_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_stations_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_stations_branch_id ON stations (branch_id);

CREATE INDEX ix_stations_created_by ON stations (created_by);

CREATE INDEX ix_stations_updated_by ON stations (updated_by);

CREATE UNIQUE INDEX uq_stations_branch_active_name ON stations (branch_id, lower(name)) WHERE status = 'Active';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261115180000_AddStations', '10.0.13');

COMMIT;
