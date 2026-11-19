START TRANSACTION;
ALTER TABLE product_tags ADD id uuid;

ALTER TABLE product_tags ADD status text NOT NULL DEFAULT 'Active';

ALTER TABLE stations ADD CONSTRAINT "AK_stations_branch_id_id" UNIQUE (branch_id, id);

CREATE TABLE product_branch_station_slots (
    product_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_product_branch_station_slots PRIMARY KEY (product_id, branch_id),
    CONSTRAINT fk_product_branch_station_slots_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_product_branch_station_slots_product_id FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE RESTRICT
);

CREATE TABLE product_routing_readiness (
    singleton boolean NOT NULL,
    verified_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_product_routing_readiness PRIMARY KEY (singleton),
    CONSTRAINT ck_product_routing_readiness_singleton CHECK (singleton)
);

CREATE TABLE product_station_assignments (
    id uuid NOT NULL,
    product_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    station_id uuid NOT NULL,
    started_at timestamp with time zone NOT NULL,
    started_by uuid,
    ended_at timestamp with time zone,
    ended_by uuid,
    CONSTRAINT pk_product_station_assignments PRIMARY KEY (id),
    CONSTRAINT fk_product_station_assignments_ended_by FOREIGN KEY (ended_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_product_station_assignments_slot FOREIGN KEY (product_id, branch_id) REFERENCES product_branch_station_slots (product_id, branch_id) ON DELETE RESTRICT,
    CONSTRAINT fk_product_station_assignments_started_by FOREIGN KEY (started_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_product_station_assignments_station FOREIGN KEY (branch_id, station_id) REFERENCES stations (branch_id, id) ON DELETE RESTRICT
);

ALTER TABLE product_tags ADD CONSTRAINT ck_product_tags_status CHECK (status IN ('Active', 'Inactive'));

CREATE INDEX ix_product_branch_station_slots_branch_id ON product_branch_station_slots (branch_id);

CREATE INDEX ix_product_station_assignments_ended_by ON product_station_assignments (ended_by);

CREATE INDEX ix_product_station_assignments_slot ON product_station_assignments (product_id, branch_id);

CREATE INDEX ix_product_station_assignments_started_by ON product_station_assignments (started_by);

CREATE INDEX ix_product_station_assignments_station ON product_station_assignments (branch_id, station_id);

CREATE UNIQUE INDEX uq_product_station_assignments_current ON product_station_assignments (product_id, branch_id) WHERE ended_at IS NULL;

DO $$
DECLARE
    application_role text := current_database() || '_app';
BEGIN
    IF to_regclass('public.product_routing_readiness') IS NOT NULL THEN
        REVOKE ALL ON TABLE public.product_routing_readiness FROM PUBLIC;
        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = application_role) THEN
            EXECUTE format('REVOKE ALL ON TABLE public.product_routing_readiness FROM %I', application_role);
            EXECUTE format('GRANT SELECT ON TABLE public.product_routing_readiness TO %I', application_role);
        END IF;
    END IF;
END $$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261117233117_ExpandTask47ProductRemediation', '10.0.13');

COMMIT;
