START TRANSACTION;
LOCK TABLE "__EFMigrationsHistory" IN ACCESS EXCLUSIVE MODE;
LOCK TABLE product_tags IN ACCESS EXCLUSIVE MODE;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM product_tags
               WHERE id IS NULL OR id = '00000000-0000-0000-0000-000000000000'
                  OR tag <> btrim(tag) OR btrim(tag) = '') THEN
        RAISE EXCEPTION 'Task 47 tag identity backfill and normalization must be verified before finalization';
    END IF;
END $$;

ALTER TABLE product_tags DROP CONSTRAINT pk_product_tags;

DROP INDEX ix_product_tags_tag;

ALTER TABLE product_tags ALTER COLUMN id SET NOT NULL;

ALTER TABLE product_tags ADD CONSTRAINT pk_product_tags PRIMARY KEY (id);

ALTER TABLE product_tags ADD CONSTRAINT ck_product_tags_normalized_label CHECK (tag = btrim(tag) AND tag <> '');

CREATE INDEX ix_product_tags_active_tag ON product_tags (tag, product_id) WHERE status = 'Active';

CREATE INDEX ix_product_tags_product_id ON product_tags (product_id);

CREATE UNIQUE INDEX uq_product_tags_active_label ON product_tags (product_id, tag) WHERE status = 'Active';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261117233501_FinalizeTask47ProductTagIdentity', '10.0.13');

COMMIT;
