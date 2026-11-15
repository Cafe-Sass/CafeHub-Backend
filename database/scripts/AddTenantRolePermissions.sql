START TRANSACTION;
ALTER TABLE tenant_roles ADD deleted_at timestamptz;

ALTER TABLE tenant_roles ADD deleted_by uuid;

CREATE UNIQUE INDEX uq_tenant_user_claims_permission ON tenant_user_claims (tenant_user_id, claim_value) WHERE claim_type = 'cafehub.permission' AND claim_value IS NOT NULL;

CREATE INDEX ix_tenant_roles_deleted_by ON tenant_roles (deleted_by);

CREATE UNIQUE INDEX uq_tenant_role_claims_permission ON tenant_role_claims (role_id, claim_value) WHERE claim_type = 'cafehub.permission' AND claim_value IS NOT NULL;

ALTER TABLE tenant_roles ADD CONSTRAINT fk_tenant_roles_deleted_by FOREIGN KEY (deleted_by) REFERENCES tenant_users (id) ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261115170000_AddTenantRolePermissions', '10.0.13');

COMMIT;
