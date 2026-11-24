START TRANSACTION;

ALTER TABLE receipt_lines ADD COLUMN wifi_code text NULL;

ALTER TABLE receipt_lines ADD CONSTRAINT ck_receipt_lines_wifi_code CHECK (wifi_code IS NULL OR (char_length(wifi_code) BETWEEN 4 AND 64 AND wifi_code ~ '^[A-Za-z0-9-]+$'));

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261124212134_AddReceiptWifiCode', '10.0.13');

COMMIT;
