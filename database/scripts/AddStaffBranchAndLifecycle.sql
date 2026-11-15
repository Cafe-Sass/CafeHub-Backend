START TRANSACTION;
ALTER TABLE tenant_users ADD branch_id uuid;

CREATE INDEX ix_tenant_users_branch_id ON tenant_users (branch_id);

ALTER TABLE tenant_users ADD CONSTRAINT ck_tenant_users_branch_scope CHECK ((user_kind = 'Staff' AND branch_id IS NOT NULL) OR (user_kind = 'Owner' AND branch_id IS NULL));

ALTER TABLE tenant_users ADD CONSTRAINT fk_tenant_users_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261112171238_AddStaffBranchAndLifecycle', '10.0.13');

COMMIT;

