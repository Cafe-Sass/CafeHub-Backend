# Database Schema Decisions

Baseline for how CafeHub persistence is organized, derived from the CafeHub PRD (CH-PRD-001 v1.0) through its requirement identifiers. The PRD text itself stays untracked; only these decisions are recorded here.

## Databases and responsibilities

- The platform uses one Master/Registry database and one Tenant database per cafe business (PRD section 9, rule 2; FR-PLT-001).
- The Master/Registry database owns platform-wide data only: tenant identity and lifecycle status, subscription state, onboarding and provisioning records, and minimal tenant database reference metadata. It never holds a cafe's operational data such as orders, menus, tables, or stock.
- Each Tenant database owns that cafe's operational data: branches, tables, menu, orders, bills and payments, WiFi packages and tokens, inventory, attendance, reporting, and tenant audit records (FR-PLT-002, FR-ANL-002).
- Technology is fixed for Sprint 1: PostgreSQL with EF Core and Npgsql on .NET 10. SQL Server is not used anywhere.

## Tenant isolation (fail closed)

- Isolation is a MUST (FR-PLT-002): one cafe must not read or modify another cafe's data.
- Tenant-scoped work reaches a tenant database only through a validated tenant context established from trusted platform metadata. A missing, invalid, or inactive context stops the operation with a safe error. There is no fallback to a shared database, the Master database, or another tenant's database.
- A caller-supplied tenant identifier alone is never treated as proof of authorization (PRD section 10.2).
- The registry stores only a secret reference per tenant. Raw connection strings stay out of source control and out of ordinary registry data (PRD section 10.2, section 11.1). Errors and logs may name a setting key but never contain a secret value.
- FR-PLT-002 requires automated tests with at least two isolated tenants proving denial at request, authorization, and data-access boundaries. At this foundation stage unit tests prove the fail-closed resolution logic with fakes; database-level proof follows in the dedicated isolation test work.

## Scope decisions that constrain the schema

- Guest-first MVP: customers order without accounts. No customer identity, login, or favourites tables exist (FR-IAM-004; PRD section 9, rule 4). Only minimal operational display information is stored per order or settlement.
- Retired quotas are not imposed by the schema: no active rule fixes a cafe at 3 branches or 15 tables, and branches and tables are ordinary rows with no quota enforcement (FR-PLT-004, FR-CAF-002, FR-CAF-003).
- Third-party POS integration is out of scope: no POS vendor identifiers, dispatch queues, or synchronization tables are designed (FR-PRN-002).
- Hybrid is a settlement mode derived at bill level from recorded Cash and Online payments. Individual payment records use only Cash or Online; no third payment-method value is ever stored (FR-PAY-004; PRD section 9, rule 12).
- Inventory is operational stock management (item administration, branch and batch receiving, manual consume/waste/adjustment movements, thresholds, low-stock and expiry alerts). No inventory tables are created in Sprint 1 (FR-INV-001 through FR-INV-007; FR-INV-007 boundary).

## Resolved contradictions

- Retired 3-branch and 15-table quotas and retired Start/Growth plan names and prices are excluded by FR-PLT-004 and FR-SUB-001; plan content is configurable data, never hard-coded schema values.
- The mandatory multi-step manual order-status workflow is retired; preparation time is passive information (PRD section 9, rule 10; FR-ORD-009), so no per-stage workflow status tables belong in the baseline.
- Hybrid as a stored individual payment method is rejected; it is derived (FR-PAY-004).
- Customer accounts from older drafts are rejected for the MVP; ordering is guest-only (FR-IAM-004).
- Payment success implying provisioning is rejected; subscription state and approval/provisioning state are separate records (FR-ONB-003).

## Refresh token sessions (Confirmed, owner decision, 2026-10-23)

Platform administrators stay signed in through long-lived refresh tokens that rotate on every use, while access tokens stay short-lived (FR-IAM-001: valid credentials establish the configured authenticated session/token). The full table design lives in `architecture/erd-master.md` (`refresh_tokens`); this section records the decisions that shape it:

- **Hash only, never the raw value.** The table stores the lowercase hex SHA-256 of the raw token and its unique index is the lookup key. A raw token is 32 random bytes, base64url-encoded, returned to the client exactly once at issuance and at each rotation. Rejected alternative: storing the raw token (a database read would be a session takeover).
- **Default lifetime 7 days.** The concrete lifetime is a validated service option (1..90 days) of the later rotation service, never a schema constant; the schema CHECK only enforces `expires_at > issued_at`.
- **Rotation on every use.** Presenting a valid active token marks it rotated (`rotated_at`, `replaced_by_id`) and issues a fresh token in the same family. Rejected alternative: non-rotating long-lived tokens (a stolen token would stay usable indefinitely).
- **Family revocation on reuse.** Presenting an already-rotated token revokes every token of its family with reason `reuse_detected` and returns the same safe rejection as any other invalid token — nothing reveals whether a token was expired, revoked, or unknown. Rejected alternative: revoking only the presented token (an attacker replaying a captured token would silently invalidate the victim's session one token at a time while keeping the family alive).
- **Master database placement.** Refresh tokens authenticate platform administrators, whose identity lives in the Master (FR-PLT-001); cross-database references are forbidden by the conventions, and tenant-side owner/staff refresh sessions are a separate future concern of `architecture/erd-tenant.md`.
- **Rejected mechanism: the Identity `user_tokens` table.** It is explicitly excluded from the Master identity model (see `architecture/erd-master.md`, `Identity model documentation`); refresh-token sessions are security state with their own lifecycle (families, rotation, revocation reasons), not framework token plumbing, so the dedicated `refresh_tokens` table is the explicit addition the ERD reserved for the Auth work.

## Tenant refresh token sessions (CFMD-323)

Tenant owner and staff sessions use a `refresh_tokens` table local to each Tenant database, following the Master table's shared columns, checks, indexes, and rotation chain design (FR-IAM-001). A Tenant token references `tenant_users` within that database; no FK or lookup crosses into the Master or another tenant database. The Tenant ERD has 9 tables including this addition.

- Store only the lowercase hex SHA-256 token hash; the raw token is returned once at issuance and rotation, never persisted. Rejected alternative: storing raw token values, which would make a database read sufficient to take over sessions.
- Use the existing `RefreshTokens:LifetimeDays` service option, rather than embedding lifetime in schema. Rejected alternative: a fixed table-level token lifetime, which cannot follow the validated service configuration.
- Rotate on every use and retain a family id across the chain. Rejected alternative: reusable non-rotating tokens, which leave a stolen token usable until expiry.
- Reuse of a rotated token revokes its family with `reuse_detected`; logout revokes with `logout`. Invalid token states share a generic rejection. Rejected alternative: revoking only the presented token, which leaves successor credentials active after replay.
- Keep this table separate from ASP.NET Identity's `user_tokens`; the latter does not represent token families, rotation links, or revocation reasons. The Master closed `revoked_reason` labels are reused. Master constraints `uq_refresh_tokens_replaced_by_id` and `ck_refresh_tokens_revocation_pair` remain **Proposed, pending owner confirmation** in both designs, as they are proposed in the merged Master ERD.
- New tenant databases receive the table through the Tenant migration sequence. Tenant databases provisioned before that migration require it to be applied; the mechanism for applying pending migrations to existing tenant databases remains an open operational item.

## TBD register (persistence-relevant)

| PRD ID | Neutral title |
|---|---|
| TBD-01 | Subscription plan catalog content: names, entitlements, limits, prices, billing periods |
| TBD-02 | Production payment provider for subscription and cafe bill payments |
| TBD-03 | Refund policy after admin rejection of a paid onboarding request |
| TBD-04 | Subscription lifecycle policy: expiry, grace, suspension, reactivation, renewal |
| TBD-05 | Online-payment confirmation flow and AI evidence-check fallback |
| TBD-06 | Cafe-bill refund and chargeback handling |
| TBD-07 | WiFi token redemption, release timing, expiry, reissue, and refund behavior |
| TBD-08 | WiFi router or platform integration, if any |
| TBD-09 | Notification provider, channels, retries, and fallbacks |
| TBD-10 | Guest display data minimization and retention or deletion rules |
| TBD-14 | Egyptian Tax Authority eReceipt and eInvoice integration decision |
| TBD-15 | Promotion and discount rules: eligibility, stacking, usage limits |
| TBD-16 | Final revenue definition and reporting eligibility rules |
| TBD-19 | Backup retention, frequency, RPO, and RTO |
| TBD-20 | Audit log retention and tamper-resistance requirements |
| TBD-22 | AI provider, model, approved context, and failure behavior |
| — | Tenant identifier source (host, header, or path); no PRD TBD number exists. Decision recorded 2026-10-23: the MVP tenant identifier source is the URL path prefix /t/{slug} (owner decision of 2026-10-19, recorded as Proposed, pending owner confirmation in the PR conversation — see architecture/architecture-decisions.md). Customer QR resolution remains a separate, undecided topic and stays open |

No entity, DbContext, or migration passes through this sub-task; this document is the decision baseline the later sub-tasks reference.

## Migrations — Master

- Migration source of truth: `src/CafeHub.Infrastructure` under `Persistence/Master/Migrations`.
- Example command (run from the repository root):

  `dotnet ef migrations add <Name> --context MasterDbContext --project src/CafeHub.Infrastructure --startup-project src/CafeHub.WebApi --output-dir Persistence/Master/Migrations`
- The connection for design-time tooling comes from the developer environment only: User Secrets (`ConnectionStrings:Master`) or the environment variable `ConnectionStrings__Master`. No connection string is ever committed; the design-time factory (`MasterDbContextFactory`) resolves only the environment variable and fails closed when it is unset.
- The first migration `20261024000514_CreateMasterIdentity` (CFMD-266) creates exactly the five Master identity tables of `architecture/erd-master.md`: `platform_admins`, `platform_admin_roles`, `platform_admin_user_roles`, `platform_admin_user_claims`, and `platform_admin_role_claims`.
- The idempotent SQL script for the migration is committed at `database/scripts/20261024000514_CreateMasterIdentity.sql`. Its line-by-line review against the ERD and the conventions recorded: exactly the five identity tables and no other table; snake_case identifiers with `pk_`/`fk_`/`uq_`/`ck_` constraint names and `ix_`/`uq_` index names; `uuid` primary keys with no database-side default (application-generated UUIDv7); `timestamptz` audit and lifecycle columns; the named `ck_` length CHECK constraints per column; self-referencing actor foreign keys (`created_by`, `updated_by`, `deleted_by`) with `ON DELETE RESTRICT`; assignment-row foreign keys with `ON DELETE CASCADE` (membership and claim rows are removable assignment state); the partial unique index `uq_platform_admins_normalized_email ... WHERE normalized_email IS NOT NULL`; and the foreign-key indexes required by `architecture/indexing-and-pagination-strategy.md` (R1). No tenant, external-login, or user-token table and no unrelated schema change appears in the script.

## Migrations — Tenant

- Migration source of truth: `src/CafeHub.Infrastructure` under `Persistence/Tenant/Migrations`.
- Example command (run from the repository root):

  `dotnet ef migrations add <Name> --context TenantDbContext --project src/CafeHub.Infrastructure --startup-project src/CafeHub.WebApi --output-dir Persistence/Tenant/Migrations`
- Tenant design-time tooling reads only the developer environment variable `ConnectionStrings__TenantDesignTime`; it targets a local developer database only and is never used at runtime. At runtime each tenant's connection is resolved through the validated tenant context and the `TenantConnections:<SecretReference>` setting convention, never through the design-time variable.
- Every connection resolves fail-closed: a missing tenant context, an inactive tenant, or an unconfigured `TenantConnections:<SecretReference>` setting stops with a safe error and never falls back to the Master connection, another tenant's connection, or a default value.
- Each Cafe has its own database, so tenant tables carry no `TenantId` discriminator. Master/Retention registry and subscription metadata do not belong in tenant tables; tenant tables never store a raw connection secret, only the identifier-independent data of one cafe.
- No tenant entity and no tenant migration exists yet; the first approved tenant entity starts the first tenant migration. All `TenantConnections` values in configuration stay out of source control.
