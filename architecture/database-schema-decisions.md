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
| — | Tenant identifier source (QR-resolved context, host, header, or path); no PRD TBD number exists for it, and it is not decided |

No entity, DbContext, or migration passes through this sub-task; this document is the decision baseline the later sub-tasks reference.

## Migrations — Master

- Migration source of truth: `src/CafeHub.Infrastructure` under `Persistence/Master/Migrations`.
- Example command (run from the repository root):

  `dotnet ef migrations add <Name> --context MasterDbContext --project src/CafeHub.Infrastructure --startup-project src/CafeHub.WebApi --output-dir Persistence/Master/Migrations`
- The connection for design-time tooling comes from the developer environment only: User Secrets (`ConnectionStrings:Master`) or the environment variable `ConnectionStrings__Master`. No connection string is ever committed.
- No migration exists today because no entity has been approved for the Master/Registry database (see the TBD register and isolated-tenant work above); the first approved entity starts the first migration.

## Migrations — Tenant

- Migration source of truth: `src/CafeHub.Infrastructure` under `Persistence/Tenant/Migrations`.
- Example command (run from the repository root):

  `dotnet ef migrations add <Name> --context TenantDbContext --project src/CafeHub.Infrastructure --startup-project src/CafeHub.WebApi --output-dir Persistence/Tenant/Migrations`
- Tenant design-time tooling reads only the developer environment variable `ConnectionStrings__TenantDesignTime`; it targets a local developer database only and is never used at runtime. At runtime each tenant's connection is resolved through the validated tenant context and the `TenantConnections:<SecretReference>` setting convention, never through the design-time variable.
- Every connection resolves fail-closed: a missing tenant context, an inactive tenant, or an unconfigured `TenantConnections:<SecretReference>` setting stops with a safe error and never falls back to the Master connection, another tenant's connection, or a default value.
- Each Cafe has its own database, so tenant tables carry no `TenantId` discriminator. Master/Retention registry and subscription metadata do not belong in tenant tables; tenant tables never store a raw connection secret, only the identifier-independent data of one cafe.
- No tenant entity and no tenant migration exists yet; the first approved tenant entity starts the first tenant migration. All `TenantConnections` values in configuration stay out of source control.
