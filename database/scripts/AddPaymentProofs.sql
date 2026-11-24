START TRANSACTION;

ALTER TABLE payments ADD COLUMN media_reference text NULL;

ALTER TABLE payments ADD COLUMN claimed_amount numeric(12,2) NULL;

ALTER TABLE payments ADD COLUMN note text NULL;

ALTER TABLE payments ADD CONSTRAINT ck_payments_media_reference_length CHECK (media_reference IS NULL OR length(media_reference) <= 512);

ALTER TABLE payments ADD CONSTRAINT ck_payments_claimed_amount_range CHECK (claimed_amount IS NULL OR claimed_amount > 0);

ALTER TABLE payments ADD CONSTRAINT ck_payments_note_length CHECK (note IS NULL OR (length(note) >= 1 AND length(note) <= 140 AND note !~ '[[:cntrl:]]'));

ALTER TABLE payments ADD CONSTRAINT ck_payments_online_proof CHECK (method <> 'Online' OR (media_reference IS NOT NULL AND claimed_amount IS NOT NULL));

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261123230300_AddPaymentProofs', '10.0.13');

COMMIT;
