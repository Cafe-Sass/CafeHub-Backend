START TRANSACTION;
CREATE TABLE menu_categories (
    id uuid NOT NULL,
    parent_id uuid,
    name text NOT NULL,
    sort_order integer NOT NULL,
    status text NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid,
    updated_by uuid,
    CONSTRAINT pk_menu_categories PRIMARY KEY (id),
    CONSTRAINT ck_menu_categories_name_length CHECK (char_length(name) BETWEEN 1 AND 64),
    CONSTRAINT ck_menu_categories_parent_not_self CHECK (parent_id <> id),
    CONSTRAINT ck_menu_categories_sort_order CHECK (sort_order >= 0),
    CONSTRAINT ck_menu_categories_status CHECK (status IN ('Active', 'Inactive')),
    CONSTRAINT fk_menu_categories_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_menu_categories_parent_id FOREIGN KEY (parent_id) REFERENCES menu_categories (id) ON DELETE RESTRICT,
    CONSTRAINT fk_menu_categories_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_menu_categories_created_by ON menu_categories (created_by);

CREATE INDEX ix_menu_categories_parent_id_sort_order_id ON menu_categories (parent_id, sort_order, id);

CREATE INDEX ix_menu_categories_updated_by ON menu_categories (updated_by);

CREATE UNIQUE INDEX uq_menu_categories_active_root_name
ON menu_categories (lower(name))
WHERE parent_id IS NULL AND status = 'Active';

CREATE UNIQUE INDEX uq_menu_categories_active_child_name
ON menu_categories (parent_id, lower(name))
WHERE parent_id IS NOT NULL AND status = 'Active';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261116061125_AddMenuCategories', '10.0.13');

COMMIT;
