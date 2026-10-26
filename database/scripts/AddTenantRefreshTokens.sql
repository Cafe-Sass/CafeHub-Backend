START TRANSACTION;
CREATE TABLE refresh_tokens (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    family_id uuid NOT NULL,
    token_hash text NOT NULL,
    issued_at timestamptz NOT NULL,
    expires_at timestamptz NOT NULL,
    rotated_at timestamptz,
    replaced_by_id uuid,
    revoked_at timestamptz,
    revoked_reason text,
    CONSTRAINT pk_refresh_tokens PRIMARY KEY (id),
    CONSTRAINT ck_refresh_tokens_expiry_after_issue CHECK (expires_at > issued_at),
    CONSTRAINT ck_refresh_tokens_revocation_pair CHECK ((revoked_at IS NULL AND revoked_reason IS NULL) OR (revoked_at IS NOT NULL AND revoked_reason IS NOT NULL)),
    CONSTRAINT ck_refresh_tokens_revoked_reason_labels CHECK (revoked_reason IS NULL OR revoked_reason IN ('logout', 'reuse_detected', 'admin_revoked')),
    CONSTRAINT ck_refresh_tokens_revoked_reason_length CHECK (revoked_reason IS NULL OR (length(revoked_reason) >= 1 AND length(revoked_reason) <= 64)),
    CONSTRAINT ck_refresh_tokens_token_hash_format CHECK (token_hash ~ '^[0-9a-f]{64}$'),
    CONSTRAINT fk_refresh_tokens_replaced_by_id FOREIGN KEY (replaced_by_id) REFERENCES refresh_tokens (id) ON DELETE RESTRICT,
    CONSTRAINT fk_refresh_tokens_user_id FOREIGN KEY (user_id) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE INDEX ix_refresh_tokens_family_id ON refresh_tokens (family_id);

CREATE INDEX ix_refresh_tokens_user_id ON refresh_tokens (user_id);

CREATE UNIQUE INDEX uq_refresh_tokens_replaced_by_id ON refresh_tokens (replaced_by_id);

CREATE UNIQUE INDEX uq_refresh_tokens_token_hash ON refresh_tokens (token_hash);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261026201216_AddTenantRefreshTokens', '10.0.13');

COMMIT;
