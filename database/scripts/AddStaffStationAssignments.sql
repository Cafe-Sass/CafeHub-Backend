START TRANSACTION;
CREATE TABLE tenant_user_stations (
    tenant_user_id uuid NOT NULL,
    station_id uuid NOT NULL,
    created_at timestamptz NOT NULL,
    CONSTRAINT pk_tenant_user_stations PRIMARY KEY (tenant_user_id, station_id),
    CONSTRAINT fk_tenant_user_stations_tenant_user_id FOREIGN KEY (tenant_user_id) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_tenant_user_stations_station_id FOREIGN KEY (station_id) REFERENCES stations (id) ON DELETE RESTRICT
);

CREATE INDEX ix_tenant_user_stations_station_id ON tenant_user_stations (station_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261116011824_AddStaffStationAssignments', '10.0.13');

COMMIT;
