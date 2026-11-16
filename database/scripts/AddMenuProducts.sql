START TRANSACTION;
ALTER TABLE cafe_settings ADD currency_code text NOT NULL DEFAULT 'egp';

CREATE TABLE products (
    id uuid NOT NULL,
    category_id uuid NOT NULL,
    station_id uuid,
    name text NOT NULL,
    description text,
    ingredients text,
    kind text NOT NULL,
    caffeine boolean NOT NULL,
    preparation_minutes integer,
    sort_order integer NOT NULL,
    status text NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid,
    updated_by uuid,
    CONSTRAINT pk_products PRIMARY KEY (id),
    CONSTRAINT ck_products_description_length CHECK (description IS NULL OR (length(description) >= 1 AND length(description) <= 1000)),
    CONSTRAINT ck_products_ingredients_length CHECK (ingredients IS NULL OR (length(ingredients) >= 1 AND length(ingredients) <= 1000)),
    CONSTRAINT ck_products_kind CHECK (kind IN ('Food', 'Drink')),
    CONSTRAINT ck_products_name_length CHECK (char_length(name) BETWEEN 1 AND 96),
    CONSTRAINT ck_products_preparation_minutes CHECK (preparation_minutes IS NULL OR (preparation_minutes >= 0 AND preparation_minutes <= 240)),
    CONSTRAINT ck_products_sort_order CHECK (sort_order >= 0),
    CONSTRAINT ck_products_status CHECK (status IN ('Active', 'Inactive')),
    CONSTRAINT fk_products_category_id FOREIGN KEY (category_id) REFERENCES menu_categories (id) ON DELETE RESTRICT,
    CONSTRAINT fk_products_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_products_station_id FOREIGN KEY (station_id) REFERENCES stations (id) ON DELETE RESTRICT,
    CONSTRAINT fk_products_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE TABLE product_addons (
    id uuid NOT NULL,
    product_id uuid NOT NULL,
    name text NOT NULL,
    price numeric(12,2) NOT NULL,
    status text NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid,
    updated_by uuid,
    CONSTRAINT pk_product_addons PRIMARY KEY (id),
    CONSTRAINT ck_product_addons_name_length CHECK (char_length(name) BETWEEN 1 AND 48),
    CONSTRAINT ck_product_addons_price_range CHECK (price >= 0 AND price <= 99999999.99),
    CONSTRAINT ck_product_addons_status CHECK (status IN ('Active', 'Inactive')),
    CONSTRAINT fk_product_addons_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_product_addons_product_id FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE RESTRICT,
    CONSTRAINT fk_product_addons_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE TABLE product_sizes (
    id uuid NOT NULL,
    product_id uuid NOT NULL,
    name text NOT NULL,
    price numeric(12,2) NOT NULL,
    is_default boolean NOT NULL,
    status text NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid,
    updated_by uuid,
    CONSTRAINT pk_product_sizes PRIMARY KEY (id),
    CONSTRAINT ck_product_sizes_name_length CHECK (char_length(name) BETWEEN 1 AND 32),
    CONSTRAINT ck_product_sizes_price_range CHECK (price >= 0 AND price <= 99999999.99),
    CONSTRAINT ck_product_sizes_status CHECK (status IN ('Active', 'Inactive')),
    CONSTRAINT fk_product_sizes_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_product_sizes_product_id FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE RESTRICT,
    CONSTRAINT fk_product_sizes_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE TABLE product_tags (
    product_id uuid NOT NULL,
    tag text NOT NULL,
    created_at timestamptz NOT NULL,
    CONSTRAINT pk_product_tags PRIMARY KEY (product_id, tag),
    CONSTRAINT ck_product_tags_tag_format CHECK (tag ~ '^[a-z0-9 -]+$'),
    CONSTRAINT ck_product_tags_tag_length CHECK (char_length(tag) BETWEEN 1 AND 32),
    CONSTRAINT ck_product_tags_tag_lowercase CHECK (tag = lower(tag)),
    CONSTRAINT fk_product_tags_product_id FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE RESTRICT
);

ALTER TABLE cafe_settings ADD CONSTRAINT ck_cafe_settings_currency_code_format CHECK (currency_code ~ '^[a-z]{3}$');

CREATE INDEX ix_product_addons_created_by ON product_addons (created_by);

CREATE INDEX ix_product_addons_product_id ON product_addons (product_id);

CREATE INDEX ix_product_addons_updated_by ON product_addons (updated_by);

CREATE INDEX ix_product_sizes_created_by ON product_sizes (created_by);

CREATE INDEX ix_product_sizes_product_id ON product_sizes (product_id);

CREATE INDEX ix_product_sizes_updated_by ON product_sizes (updated_by);

CREATE INDEX ix_product_tags_tag ON product_tags (tag);

CREATE INDEX ix_products_category_id ON products (category_id);

CREATE INDEX ix_products_created_by ON products (created_by);

CREATE INDEX ix_products_station_id ON products (station_id);

CREATE INDEX ix_products_updated_by ON products (updated_by);

CREATE UNIQUE INDEX uq_product_sizes_active_default
ON product_sizes (product_id)
WHERE is_default AND status = 'Active';

CREATE UNIQUE INDEX uq_product_sizes_active_name
ON product_sizes (product_id, lower(name))
WHERE status = 'Active';

CREATE UNIQUE INDEX uq_product_addons_active_name
ON product_addons (product_id, lower(name))
WHERE status = 'Active';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261116122028_AddMenuProducts', '10.0.13');

COMMIT;

