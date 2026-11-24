START TRANSACTION;

ALTER TABLE orders DROP CONSTRAINT ck_orders_service_type;

ALTER TABLE orders ALTER COLUMN table_id DROP NOT NULL;

ALTER TABLE bills ALTER COLUMN table_id DROP NOT NULL;

ALTER TABLE order_items ADD COLUMN wifi_package_id uuid NULL;

ALTER TABLE order_items ADD COLUMN wifi_duration_minutes integer NULL;

ALTER TABLE order_items ADD COLUMN wifi_speed_mbps numeric(9,2) NULL;

ALTER TABLE orders ADD CONSTRAINT ck_orders_service_table CHECK ((service_type = 'WifiOnly' AND table_id IS NULL) OR (service_type <> 'WifiOnly' AND table_id IS NOT NULL));

ALTER TABLE orders ADD CONSTRAINT ck_orders_service_type CHECK (service_type IN ('DineIn', 'Takeaway', 'WifiOnly'));

ALTER TABLE order_items ADD CONSTRAINT ck_order_items_product_or_wifi CHECK ((product_id IS NULL) <> (wifi_package_id IS NULL));

ALTER TABLE order_items ADD CONSTRAINT ck_order_items_wifi_snapshot CHECK (wifi_package_id IS NULL OR (wifi_duration_minutes IS NOT NULL AND wifi_duration_minutes BETWEEN 1 AND 43200));

CREATE INDEX ix_order_items_wifi_package_id ON order_items (wifi_package_id);

ALTER TABLE order_items ADD CONSTRAINT fk_order_items_wifi_package_id FOREIGN KEY (wifi_package_id) REFERENCES wifi_packages (id) ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261124195948_AddWifiOrderLines', '10.0.13');

COMMIT;
