START TRANSACTION;

CREATE TABLE refund_requests (
    id uuid NOT NULL,
    bill_id uuid NOT NULL,
    payment_id uuid NOT NULL,
    order_id uuid NULL,
    branch_id uuid NOT NULL,
    public_reference text NOT NULL,
    idempotency_key text NOT NULL,
    status text NOT NULL,
    reason text NOT NULL,
    details text NULL,
    claimed_amount numeric(12,2) NOT NULL,
    guest_display_name text NULL,
    assigned_user_id uuid NULL,
    resolution_note text NULL,
    decided_by_user_id uuid NULL,
    decided_at timestamptz NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    created_by uuid NULL,
    updated_by uuid NULL,
    CONSTRAINT pk_refund_requests PRIMARY KEY (id),
    CONSTRAINT ck_refund_requests_public_reference_format CHECK (char_length(public_reference) = 22 AND public_reference ~ '^[A-Za-z0-9_-]{22}$'),
    CONSTRAINT ck_refund_requests_idempotency_key_format CHECK (char_length(idempotency_key) BETWEEN 8 AND 64 AND idempotency_key ~ '^[A-Za-z0-9_-]+$'),
    CONSTRAINT ck_refund_requests_status CHECK (status IN ('Open', 'InReview', 'Approved', 'Rejected')),
    CONSTRAINT ck_refund_requests_reason_length CHECK (char_length(btrim(reason)) BETWEEN 3 AND 256),
    CONSTRAINT ck_refund_requests_details_length CHECK (details IS NULL OR char_length(details) <= 2000),
    CONSTRAINT ck_refund_requests_claimed_amount CHECK (claimed_amount > 0),
    CONSTRAINT ck_refund_requests_decision_pair CHECK ((status IN ('Approved', 'Rejected') AND resolution_note IS NOT NULL AND char_length(btrim(resolution_note)) > 0 AND decided_by_user_id IS NOT NULL AND decided_at IS NOT NULL) OR (status IN ('Open', 'InReview') AND resolution_note IS NULL AND decided_by_user_id IS NULL AND decided_at IS NULL)),
    CONSTRAINT fk_refund_requests_assigned_user_id FOREIGN KEY (assigned_user_id) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_refund_requests_bill_id FOREIGN KEY (bill_id) REFERENCES bills (id) ON DELETE RESTRICT,
    CONSTRAINT fk_refund_requests_branch_id FOREIGN KEY (branch_id) REFERENCES branches (id) ON DELETE RESTRICT,
    CONSTRAINT fk_refund_requests_created_by FOREIGN KEY (created_by) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_refund_requests_decided_by_user_id FOREIGN KEY (decided_by_user_id) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_refund_requests_order_id FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE RESTRICT,
    CONSTRAINT fk_refund_requests_payment_id FOREIGN KEY (payment_id) REFERENCES payments (id) ON DELETE RESTRICT,
    CONSTRAINT fk_refund_requests_updated_by FOREIGN KEY (updated_by) REFERENCES tenant_users (id) ON DELETE RESTRICT
);

CREATE TABLE refund_request_attachments (
    id uuid NOT NULL,
    refund_request_id uuid NOT NULL,
    media_key text NOT NULL,
    content_type text NOT NULL,
    size_bytes bigint NOT NULL,
    sha256 text NOT NULL,
    created_at timestamptz NOT NULL,
    CONSTRAINT pk_refund_request_attachments PRIMARY KEY (id),
    CONSTRAINT ck_refund_request_attachments_media_key_length CHECK (char_length(media_key) BETWEEN 1 AND 512),
    CONSTRAINT ck_refund_request_attachments_content_type CHECK (content_type IN ('image/jpeg', 'image/png', 'image/webp', 'application/pdf')),
    CONSTRAINT ck_refund_request_attachments_size CHECK (size_bytes > 0),
    CONSTRAINT ck_refund_request_attachments_sha256 CHECK (sha256 ~ '^[0-9a-f]{64}$'),
    CONSTRAINT fk_refund_request_attachments_refund_request_id FOREIGN KEY (refund_request_id) REFERENCES refund_requests (id) ON DELETE RESTRICT
);

CREATE TABLE refund_request_events (
    id uuid NOT NULL,
    refund_request_id uuid NOT NULL,
    kind text NOT NULL,
    from_status text NULL,
    to_status text NULL,
    actor_kind text NOT NULL,
    actor_user_id uuid NULL,
    note text NULL,
    occurred_at timestamptz NOT NULL,
    CONSTRAINT pk_refund_request_events PRIMARY KEY (id),
    CONSTRAINT ck_refund_request_events_kind CHECK (kind IN ('Submitted', 'Assigned', 'StatusChanged')),
    CONSTRAINT ck_refund_request_events_from_status CHECK (from_status IS NULL OR from_status IN ('Open', 'InReview', 'Approved', 'Rejected')),
    CONSTRAINT ck_refund_request_events_to_status CHECK (to_status IS NULL OR to_status IN ('Open', 'InReview', 'Approved', 'Rejected')),
    CONSTRAINT ck_refund_request_events_actor_kind CHECK (actor_kind IN ('Guest', 'Staff')),
    CONSTRAINT ck_refund_request_events_actor_pair CHECK ((actor_kind = 'Guest' AND actor_user_id IS NULL) OR (actor_kind = 'Staff' AND actor_user_id IS NOT NULL)),
    CONSTRAINT fk_refund_request_events_actor_user_id FOREIGN KEY (actor_user_id) REFERENCES tenant_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_refund_request_events_refund_request_id FOREIGN KEY (refund_request_id) REFERENCES refund_requests (id) ON DELETE RESTRICT
);

CREATE INDEX ix_refund_request_attachments_refund_request_id ON refund_request_attachments (refund_request_id);
CREATE INDEX ix_refund_request_events_actor_user_id ON refund_request_events (actor_user_id);
CREATE INDEX ix_refund_request_events_refund_request_id ON refund_request_events (refund_request_id);
CREATE INDEX ix_refund_requests_assigned_user_id ON refund_requests (assigned_user_id);
CREATE INDEX ix_refund_requests_bill_id ON refund_requests (bill_id);
CREATE INDEX ix_refund_requests_branch_id ON refund_requests (branch_id);
CREATE INDEX ix_refund_requests_created_by ON refund_requests (created_by);
CREATE INDEX ix_refund_requests_decided_by_user_id ON refund_requests (decided_by_user_id);
CREATE INDEX ix_refund_requests_order_id ON refund_requests (order_id);
CREATE INDEX ix_refund_requests_payment_id ON refund_requests (payment_id);
CREATE INDEX ix_refund_requests_updated_by ON refund_requests (updated_by);
CREATE UNIQUE INDEX uq_refund_requests_branch_idempotency_key ON refund_requests (branch_id, idempotency_key);
CREATE UNIQUE INDEX uq_refund_requests_public_reference ON refund_requests (public_reference);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261126233034_AddRefundRequests', '10.0.13');

COMMIT;
