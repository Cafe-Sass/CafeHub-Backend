# Indexing and Pagination Strategy

Binding rules for indexes and list queries that every later CafeHub entity configuration and list query follows. Companion documents: `architecture/database-conventions.md` (naming, typing, and the expression-index mechanism), `architecture/erd-master.md` and `architecture/erd-tenant.md` (the tables, foreign keys, and unique constraints this document indexes and justifies), `architecture/database-schema-decisions.md`, and `architecture/architecture-decisions.md`. The companion views strategy is a separate document of the same story family (`CFMD-256`) and is out of scope here. Requirement identifiers cited in this document exist in the PRD and were verified there before use; the PRD text itself stays untracked.

Two kinds of content are distinguished throughout:

- **Defined already** — rows and rules that restate what `architecture/erd-master.md` and `architecture/erd-tenant.md` already declare. Their names, columns, and predicates are copied from those documents, not invented here; each is justified against the requirement it enforces.
- **Proposed, pending owner confirmation** — new index and pagination design choices made by this document. Every proposal carries a why and a rejected alternative, and the full proposal list belongs to the story review, not to entity code.

This sub-task is documentation only: no entity, migration, DbContext, or index is created in code. The entity-mapping stories for the two ERDs (`CFMD-79` for Master, `CFMD-74` for Tenant) are the work that realizes these indexes through explicit EF Core configuration (`HasDatabaseName`, `HasConstraintName`) and raw SQL only where the conventions require it (for example the expression indexes below). The pagination request contract itself — page-size request shape, cursor encoding, endpoint semantics — is a later story; this document fixes the per-list pagination decision and the index that serves it.

## 1. Indexing rules

Each rule is stated as the rule CafeHub follows, with the why. The rules themselves restate this story's acceptance criteria; the sub-decisions inside them (such as R5's purpose-qualifier naming) are marked **Proposed, pending owner confirmation** and are confirmed at this story's review.

### R1 — Every foreign key is indexed

Every foreign-key column obtains a supporting non-unique `ix_` index, unless an existing constraint on the same table already provides a usable leftmost prefix for it — that coverage is then stated explicitly in the index list instead of adding a duplicate index (R4, R6).

**Why.** PostgreSQL creates a foreign key constraint without an index on the referencing column; the referencing side is not indexed automatically. An unindexed foreign key makes every reverse lookup ("all children of parent X", "the child row of parent X") scan the heap. Under the confirmed logical-deletion policy parent rows are never physically deleted, so the referential-integrity trigger scans are rarely exercised — but the reverse lookups are a real, recurring query family in this schema: the payment attempts of one onboarding request, the attempts of one provisioning job, the periods of one subscription, the membership rows of one role. An index design that documents the reverse lookup per foreign key keeps those access paths visible at review time instead of letting sequential scans accumulate silently.

**Rejected alternative.** Index foreign keys reactively, after a measured slowdown: the first casualty is production latency during a support scenario, and retrofitting later hides which queries were expected to use the index. Small per-tenant tables could genuinely survive without it, but making the rule conditional on table size turns every FK into a per-table judgment call that review cannot verify mechanically.

### R2 — Business invariants are enforced by unique constraints, not only by application checks

Every business invariant of the "at most one" and "exactly one" shape is carried by a declared unique constraint (plain, composite, partial, or expression) at the database boundary. No such invariant depends on application validation alone.

**Why.** Application checks run sequentially per request. Two concurrent writes can both pass an application-only check and both commit; the invariant is then violated silently and only discovered by later support queries. The constraint holds under any writer, including migrations written by hand. The merged ERDs already declare the invariants; section 2 and section 3 of the index list name each one and the requirement it enforces. This document proposes no new unique constraint — it indexes the ones declared there and states which existing constraint must not be duplicated.

**Rejected alternative.** Library- or handler-level uniqueness verification only: concurrency is exactly the case the PRD calls out (safe behavior under concurrent registration attempts is part of the duplicate-registration requirement, FR-ONB-004), and an application-only guard cannot meet it.

### R3 — Partial indexes serve the active or pending subsets

Where a query reads a bounded subset of a table that grows without bound, the supporting index is a partial index: the predicate carves out the subset, the key columns carry the subset's own filter/sort/lookup work.

**Why.** Under the confirmed logical-deletion policy, most platform tables keep every row forever (every registration, payment attempt, audit record, and provisioning attempt persists). The rows a live query touches are the current subset: pending reviews, jobs awaiting retry, the running period of a subscription, open renewal requests. A partial index keeps its keyed rows and its write amplification (R7) proportional to the live subset rather than to the total history, and because its key set is the subset itself, it can serve subset queries as a direct scan — the index effectively is the queue.

**Rejected alternative.** Full secondary indexes on `(status, sort column)`: for any one status, the entries for every other status are dead weight the index must carry, housekeep, and skip. A partial index per consumed subset is smaller and states the subset intent in a reviewable predicate.

### R4 — Composite indexes order equality-filter columns first, then the sort column

A composite index lists its columns as: equality-filter columns first, then the columns the result is sorted by.

**Why.** A B-tree serves a query only through its leftmost prefix. Equality conditions on leading columns localize the scan window, and a final sort column lets the index deliver rows already in result order — no sort node, no heap ordering. Reversing the order (sort column first) localizes nothing for a filtered lookup; the planner can then use the index only when the filter already pins one contiguous sort range. The two shapes answer different queries, and the common CafeHub shape is filter-then-sort.

**Rejected alternative.** One-column indexes on the filter and the sort column each: PostgreSQL would combine them into a bitmap plus a sort node, and the combination pays off less and less as the filtered result set grows.

### R5 — Index and constraint naming follows `architecture/database-conventions.md`

Constraint names use the fixed prefixes `pk_`, `fk_`, `uq_`; check constraints use `ck_`; all index names use `uq_` (unique index) or `ix_` (non-unique index). Names stay under PostgreSQL's 63-character identifier limit with short qualifiers.

**Proposed, pending owner confirmation.** Composite or subset-scoped index names may carry a short purpose qualifier when the column list would bloat or mislead: `ix_platform_audit_records_subject` for `(subject_type, subject_id, recorded_at)`, `ix_provisioning_jobs_retry_queue` for the awaiting-retry subset, matching the precedent of `uq_subscription_renewal_requests_open` in `architecture/erd-master.md`.

**Why.** The prefixes make object kinds unambiguous in database logs and migration reviews. Purpose qualifiers exist because the column list for a subset index — whose shape is a policy choice rather than a data key — is either too long or says the wrong thing about what the key is for.

**Rejected alternative.** Fully generated names from a naming-convention package (already rejected in the conventions document); a non-qualifier rule that would force `ix_platform_audit_records_subject_type_subject_id_recorded_at` and similar fragile slabs.

### R6 — No index without the query or the invariant it serves

Every index and every constraint named in this document serves either a named query (access-path indexes: reverse lookups, list paging, subset scans) or a named business invariant (unique and maintained constraints). An index that names neither is not added.

**Why.** R7 makes every index a recurring write cost. "In case we later page by X" is the exact access-path ambiguity this document exists to remove; speculative entries make review meaningless and hide which indexes actually carry the workload.

**Rejected alternative.** The inconvenience is discoverability: an index the first author remembers but no document records; the fix is this section — the list is the record.

### R7 — Every index has a write-amplification cost

Each maintained index adds work to every `INSERT` and to every `UPDATE` that touches its key columns; every index also grows the per-database housekeeping work. The added cost is accepted only where a named query or invariant justifies it, and is bound down where possible: partial indexes (R3) key only the live subset.

**Why.** The write-heavy tables under CafeHub's confirmed decisions are append-shaped (payments settle and stop, periods close, audit records insert once), but several update in place (jobs retry with new status values, onboarding requests make state transitions, tenant rows change lifecycle). Each index multiplies that cost. Keeping the index list to the reviewed inventory in sections 2 and 3 is the way write amplification stays predictable and policies like the retry queue stay cheap.

**Rejected alternative.** "Index everything, measure later": the cost of a wrong index is paid on every write until someone notices, and pruning an index later requires a measured baseline the project never captured.

### R8 — Measure with `EXPLAIN` before adding a second index for the same query

When a query can already be served by an entry in this inventory — through a leftmost prefix, a backward scan, or a subset predicate — a candidate second index for that query is measured with `EXPLAIN (ANALYZE)` against representative data before it is added. A measured win (or a plan the old index cannot produce at all) is the admission ticket for a changed or additional index, and the change is recorded here.

**Why.** PostgreSQL can serve many query shapes from several existing indexes given its backward-scan capability and the composite indexes this inventory already declares. Performance work that skips measurement adds write amplification (R7) without moving any first-page latency, and it regularly adds cost for latency the query never lacked in the first place. Measuring before the index exists is what makes the evidence real instead of retrospective.

**Rejected alternative.** Auto-suggest tooling, or adding a covering index for everything a query might touch: both are speculative widening under R6/R7.

### R9 — No index on a password-hash column, in any predicate or covering index

No index is defined on `onboarding_requests.owner_password_hash`, `platform_admins.password_hash`, or `tenant_users.password_hash`, and no composite or covering index ever includes one. Lookups on identity tables use the mechanism's `normalized_user_name` / `normalized_email` index shapes only; a credential hash is verified through the mechanism's verifier, never looked up by value.

**Why.** A hash is never a query key in any CafeHub flow — its reads go through the framework's password verification against a row already found by an indexed identity column. Indexing it would widen the storage surface of a secret-bearing column (log and index pages would carry the value redundantly) for exactly zero serving queries. The sensitive-column rules in `architecture/erd-master.md` already narrow the hash's read surface; this rule keeps that narrowing out of the index layer as well.

**Rejected alternative.** A "find by hash" diagnostic index to trace a credential across flows — this contradicts the one-write-one-read contract the ERD records for the hash and invites a query pattern that should not exist.

## 2. Index list — Master database

Reading rules for the list:

- Each row is either a **Defined already** constraint or index from `architecture/erd-master.md`, or a **Proposed, pending owner confirmation** index this document adds. The Why cell says which.
- `pk_` rows are listed because the primary key is the default access path for every single-row fetch by id, and (on append-heavy tables) the keyset cursor anchor for pagination (see section 4).
- `ck_` check constraints shape filters but are not indexes; they are listed in the ERDs and are not rows here, except where an index predicate reuses a status subset they constrain.
- Where an existing composite index provides a usable leftmost prefix for an FK column, no separate index is proposed; the coverage is stated inside that row instead ("leftmost prefix doubles as the FK index"). Duplicating it would violate R6/R8.

| Table | Index or constraint name | Columns and kind | Query or rule served | Why |
|---|---|---|---|---|
| platform_admins | `pk_platform_admins` | `id` | single-row fetch by key | every account lookup and FK join starts here |
| platform_admins | `uq_platform_admins_normalized_user_name` | `normalized_user_name`, unique | sign-in lookup by username, and the "one admin per username" invariant | the Identity mechanism's normalized-uniqueness contract, and the index the login lookup itself reads |
| platform_admins | `uq_platform_admins_normalized_email` | `normalized_email`, unique, partial `WHERE normalized_email IS NOT NULL` | admin lookup by mailbox (review and reset flows) | Defined already (ERD proposal): this table holds platform admin identities, owners are never rows here, so two admins sharing one mailbox is always a defect; the partial form states the mail-scoped intent and tolerates NULL addresses honestly |
| platform_admins | `ix_platform_admins_created_by` | `created_by` (self-FK), single-column | reverse lookup "accounts created by admin X" (rare Support inspection) | Proposed, pending owner confirmation. R1: PostgreSQL does not index FKs automatically; parent rows are never physically deleted, so the query served is the reverse lookup |
| platform_admins | `ix_platform_admins_updated_by` | `updated_by` (self-FK), single-column | reverse lookup "edits attributed to admin X" (rare) | Proposed, pending owner confirmation. R1 as above |
| platform_admins | `ix_platform_admins_deleted_by` | `deleted_by` (self-FK), single-column | reverse lookup "deactivations performed by admin X" | Proposed, pending owner confirmation. R1 as above |
| platform_admin_roles | `pk_platform_admin_roles` | `id` | single-row fetch by key | role resolution by id |
| platform_admin_roles | `uq_platform_admin_roles_normalized_name` | `normalized_name`, unique | role lookup by name during authorization, and the "one role per name" invariant | the mechanism's normalized-uniqueness contract |
| platform_admin_roles | `ix_platform_admin_roles_created_by` | `created_by` (FK), single-column | reverse lookup "roles created by admin X" (rare) | Proposed, pending owner confirmation. R1 as above |
| platform_admin_roles | `ix_platform_admin_roles_updated_by` | `updated_by` (FK), single-column | reverse lookup "role edits attributed to admin X" (rare) | Proposed, pending owner confirmation. R1 as above |
| platform_admin_user_roles | `pk_platform_admin_user_roles` | `id` | single-row fetch by key | membership fetch by id |
| platform_admin_user_roles | `uq_platform_admin_user_roles_user_role` | `platform_admin_id, role_id`, unique, composite | invariant "one membership per user-role pair"; leftmost prefix doubles as the FK index for `platform_admin_id` | duplicate memberships silently inflate every authorization check; the composite also answers "roles of user X" |
| platform_admin_user_roles | `ix_platform_admin_user_roles_role_id` | `role_id`, single-column | members of role X | Proposed, pending owner confirmation. FK rule R1; needed because the composite above only prefixes on `platform_admin_id` |
| platform_admin_user_claims | `pk_platform_admin_user_claims` | `id` | single-row fetch by key | claim fetch by id |
| platform_admin_user_claims | `ix_platform_admin_user_claims_platform_admin_id` | `platform_admin_id`, single-column | claims of user X | Proposed, pending owner confirmation. R1; the read that authorization evaluation performs per user |
| platform_admin_role_claims | `pk_platform_admin_role_claims` | `id` | single-row fetch by key | claim fetch by id |
| platform_admin_role_claims | `ix_platform_admin_role_claims_role_id` | `role_id`, single-column | claims of role X | Proposed, pending owner confirmation. R1; the read that authorization evaluation performs per role |
| plans | `pk_plans` | `id` | single-row fetch by key | plan fetch by id |
| plans | `uq_plans_code` | `code`, unique | lookup by stable code reference, and the "one plan per code" invariant | stable machine-readable reference for code paths (ERD proposal); also anchors the catalog list's sort (section 4) |
| plans | `ix_plans_created_by` | `created_by` (FK), single-column | reverse lookup "plans created by admin X" (rare) | Proposed, pending owner confirmation. R1 as above |
| plans | `ix_plans_updated_by` | `updated_by` (FK), single-column | reverse lookup "plan edits attributed to admin X" (rare) | Proposed, pending owner confirmation. R1 as above |
| plan_versions | `pk_plan_versions` | `id` | single-row fetch by key | version fetch by id, the period-selection lookup |
| plan_versions | `uq_plan_versions_plan_version` | `plan_id, version_number`, unique, composite | invariant "per-plan version sequence" (owner-approved per-plan versioning, FR-SUB-001); leftmost prefix doubles as the FK index for `plan_id` | a plan's numbering is scoped to itself and its referenced history is never disturbed; the same composite serves "versions of plan X, in version order" |
| plan_versions | `ix_plan_versions_created_by` | `created_by` (FK), single-column | reverse lookup "versions created by admin X" (rare) | Proposed, pending owner confirmation. R1 as above |
| plan_versions | `ix_plan_versions_updated_by` | `updated_by` (FK), single-column | reverse lookup "version updates attributed to admin X" (rare) | Proposed, pending owner confirmation. R1 as above |
| plan_entitlements | `pk_plan_entitlements` | `id` | single-row fetch by key | entitlement fetch by id |
| plan_entitlements | `uq_plan_entitlements_key` | `plan_version_id, entitlement_key`, unique, composite | invariant "one value per entitlement key per version" (FR-PLT-004 data model); leftmost prefix doubles as the FK index | a version's entitlement set is immutable data, so its access path is a stable composite scan for "entitlements of version X" |
| onboarding_requests | `pk_onboarding_requests` | `id` | single-row fetch by key | request fetch by id, review and approval flows |
| onboarding_requests | `uq_onboarding_requests_slug` | `tenant_slug`, unique, partial `WHERE onboarding_status NOT IN ('Approved', 'Rejected', 'Cancelled')` | live-request slug claim; also the registration-time duplicate-slug pre-check | Defined already (ERD slug rules): two live requests cannot race for the same slug, terminal requests release it, and lowercase storage is CHECK-enforced so the index is plain, not an expression |
| onboarding_requests | `uq_onboarding_requests_public_owner_email` | `lower(owner_email)`, unique expression index, partial `WHERE created_via = 'PublicRegistration'` | enforces the permanent public-registration duplicate rule (FR-ONB-004) and serves the application duplicate pre-check lookup | Defined already (ERD, per the conventions' expression-index mechanism): the filter is the creation path, not a status subset, because the guard is permanent (rejections and cancellations do not bypass it); the Support path stays outside so the authorized additional-cafe flow keeps working and is audited. A `CHECK` over the same filter cannot compare text case-insensitively, so only the expression index enforces it |
| onboarding_requests | `ix_onboarding_requests_plan_version_id` | `plan_version_id`, single-column | requests on one plan version (catalog analytics before retiring a version) | Proposed, pending owner confirmation. R1; version retirement must not strand referenced requests |
| onboarding_requests | `ix_onboarding_requests_created_by` | `created_by` (FK), single-column | Support queue "requests created by admin X" | Proposed, pending owner confirmation. R1; the auditable Support exception (FR-ONB-004) needs its per-actor traceability |
| onboarding_requests | `ix_onboarding_requests_reviewed_by` | `reviewed_by` (FK), single-column | "requests reviewed by admin X" | Proposed, pending owner confirmation. R1; review accountability remains queryable per approval actor |
| onboarding_requests | `ix_onboarding_requests_updated_by` | `updated_by` (FK), single-column | reverse lookup "edits attributed to admin X" (rare) | Proposed, pending owner confirmation. R1 as above |
| onboarding_requests | `ix_onboarding_requests_review_queue` | `created_at`, single-column, partial `WHERE onboarding_status = 'PaidAwaitingReview'` | the review queue list, oldest pending request first (the list paging decision, section 4) | Proposed, pending owner confirmation. R3: live pending rows are a small bounded subset; the partial index is the queue itself, so offset reads never touch history |
| tenants | `pk_tenants` | `id` | single-row fetch by key | tenant fetch by id, the registry anchor |
| tenants | `uq_tenants_slug` | `slug`, unique, plain | tenant lookup by URL slug (the MVP tenant identifier is the `t/{slug}` path prefix) and the "one tenant per slug" invariant | Defined already (ERD, per the conventions' lowered-storage mechanism): a plain unique index, not a `lower(slug)` expression, because lowercase storage is already CHECK-enforced; expression would only duplicate the guarantee |
| tenants | `uq_tenants_onboarding_request_id` | `onboarding_request_id`, unique | invariant exactly one tenant per request; reverse lookup "tenant created from request X" (detail views) | Defined already (ERD, from the owner-approved provisioning sequence, FR-ONB-005): the FK lives on `tenants`, so a rejected request yields no tenant row at all |
| tenants | `ix_tenants_created_by` | `created_by` (FK), single-column | "tenants approved by admin X" | Proposed, pending owner confirmation. R1 as above |
| tenants | `ix_tenants_updated_by` | `updated_by` (FK), single-column | reverse lookup "edits attributed to admin X" (rare) | Proposed, pending owner confirmation. R1 as above |
| tenant_databases | `pk_tenant_databases` | `id` | single-row fetch by key | database-reference fetch by id |
| tenant_databases | `uq_tenant_databases_tenant_id` | `tenant_id`, unique | "the database reference of tenant X" | Defined already (ERD): one reference per tenant, never two; the reverse lookup is the platform-metadata read that carries the secret reference for connection resolution |
| tenant_databases | `uq_tenant_databases_secret_reference` | `secret_reference`, unique | lookup by secret reference (reconcile flows), and key-collision prevention across `TenantConnections:` settings | Defined already (ERD): the value is a configuration key tail, not a secret; uniqueness avoids accidental collisions across configuration |
| tenant_databases | `ix_tenant_databases_created_by` | `created_by` (FK), single-column | reverse lookup "references created by admin X" (rare) | Proposed, pending owner confirmation. R1 as above |
| tenant_databases | `ix_tenant_databases_updated_by` | `updated_by` (FK), single-column | reverse lookup "edits attributed to admin X" (rare) | Proposed, pending owner confirmation. R1 as above |
| subscriptions | `pk_subscriptions` | `id` | single-row fetch by key | subscription fetch by id |
| subscriptions | `uq_subscriptions_onboarding_request_id` | `onboarding_request_id`, unique | "the subscription of request X" | Defined already (ERD anchor, FR-ONB-002/FR-SUB-002): exactly one logical subscription anchored by the initial successful payment |
| subscriptions | `uq_subscriptions_tenant_id` | `tenant_id`, unique | "the subscription of tenant X" (overview listing) | Defined already (ERD anchor, FR-ONB-005): exactly one logical subscription per tenant; the pair of anchors is kept honest by the ERD's ownership CHECK, which is not an index and is not duplicated here |
| subscriptions | `ix_subscriptions_created_by` | `created_by` (FK), single-column | reverse lookup "subscriptions created by admin X" (rare; rows are system-created at payment success) | Proposed, pending owner confirmation. R1 as above |
| subscriptions | `ix_subscriptions_updated_by` | `updated_by` (FK), single-column | reverse lookup "edits attributed to admin X" (rare) | Proposed, pending owner confirmation. R1 as above |
| subscription_periods | `pk_subscription_periods` | `id` | single-row fetch by key | period fetch by id (renewal-result reads) |
| subscription_periods | `uq_subscription_periods_period_number` | `subscription_id, period_number`, unique, composite | invariant "per-subscription 1..n period history" (FR-SUB-003); leftmost prefix doubles as the FK index | Defined already: the numbering is independent of timestamps; the composite also serves "period history of subscription X in order", the list paging decision in section 4 |
| subscription_periods | `uq_subscription_periods_active` | `subscription_id`, unique, partial `WHERE period_status = 'Active'` | "the current period of subscription X" and the exactly-one-running-period invariant | Defined already (ERD): at most one running period at a time; the partial form keys only live periods and serves renewal-proposal building |
| subscription_periods | `uq_subscription_periods_renewal_request` | `renewal_request_id`, unique | reverse lookup "period created by renewal X" | Defined already (ERD, FR-SUB-004): one approved renewal can never produce two periods, closing the double-billing path |
| subscription_periods | `ix_subscription_periods_plan_version_id` | `plan_version_id`, single-column | periods referencing one version (retirement checks) | Proposed, pending owner confirmation. R1; retiring a version requires a structured reference look-up; version immutability (FR-SUB-001) keeps the index stable |
| subscription_periods | `ix_subscription_periods_created_by` | `created_by` (FK), single-column | reverse lookup (rows are system-written at payment success, so rarely exercised) | Proposed, pending owner confirmation. R1 as above |
| subscription_periods | `ix_subscription_periods_updated_by` | `updated_by` (FK), single-column | reverse lookup (periods are never rewritten by ordinary workflows, so rarely exercised) | Proposed, pending owner confirmation. R1 as above |
| subscription_renewal_requests | `pk_subscription_renewal_requests` | `id` | single-row fetch by key | renewal fetch by id |
| subscription_renewal_requests | `uq_subscription_renewal_requests_open` | `subscription_id`, unique, partial `WHERE status IN ('PendingOwnerApproval', 'Approved')` | "the open renewal of subscription X" and the in-flight invariant | Defined already (ERD, FR-SUB-004): at most one renewal pending or approved-but-unpaid per subscription, so duplicate requests, retries, or callbacks cannot double-charge a cycle |
| subscription_renewal_requests | `ix_subscription_renewal_requests_subscription_id` | `subscription_id`, single-column, full | per-subscription renewal history (all statuses) | Proposed, pending owner confirmation. R1; this is deliberately a distinct index from the partial above, which by design covers only the open subset — history pages read every status |
| subscription_renewal_requests | `ix_subscription_renewal_requests_plan_version_id` | `plan_version_id`, single-column | proposals of one version | Proposed, pending owner confirmation. R1; price and currency verification against the proposed version (FR-SUB-001/FR-SUB-002) reads this way |
| subscription_renewal_requests | `ix_subscription_renewal_requests_created_by` | `created_by` (FK), single-column | reverse lookup (proposal creation is system-driven, rarely exercised) | Proposed, pending owner confirmation. R1 as above |
| subscription_renewal_requests | `ix_subscription_renewal_requests_updated_by` | `updated_by` (FK), single-column | reverse lookup (decision transitions are system-driven, rarely exercised) | Proposed, pending owner confirmation. R1 as above |
| platform_payment_transactions | `pk_platform_payment_transactions` | `id` | single-row fetch by key | every payment lookup starts here (idempotency hits, settlement updates) |
| platform_payment_transactions | `uq_platform_payment_transactions_key` | `idempotency_key`, unique | idempotency lookup "is this intended payment already recorded, and which result does its row carry?" | Defined already (FR-SUB-002): the uniqueness forces a duplicate call or callback to reuse the row instead of creating a second payment record |
| platform_payment_transactions | `ix_platform_payment_transactions_onboarding_request_id` | `onboarding_request_id`, single-column | payment attempts of one onboarding request | Proposed, pending owner confirmation. R1; requests may carry several attempts (FR-ONB-004 retries reuse the request row), and the request's payment state derives from its rows |
| platform_payment_transactions | `ix_platform_payment_transactions_renewal_request_id` | `renewal_request_id`, single-column | payments linked to one approved renewal request | Proposed, pending owner confirmation. R1; renewal payment processing must trace to the corresponding approved request (FR-SUB-002) |
| platform_payment_transactions | `ix_platform_payment_transactions_updated_by` | `updated_by` (FK), single-column | reverse lookup (rows are state-full across initiation to settlement) | Proposed, pending owner confirmation. R1 as above; `created_by` on this table carries no FK in the ERD (system-written), so it gets no FK-rule index |
| platform_payment_transactions | `ix_platform_payment_transactions_created_at` | `created_at, id`, composite | the transactions list's keyset paging order (newest first, section 4) | Proposed, pending owner confirmation. R3 has no useful subset to carve out here, so the cost is R7-bounded: rows are written once at initiation and the key columns never change afterward |
| provisioning_jobs | `pk_provisioning_jobs` | `id` | single-row fetch by key | job fetch by id (worker pickup by exact job row) |
| provisioning_jobs | `uq_provisioning_jobs_tenant_id` | `tenant_id`, unique | "the provisioning job of tenant X" and the one-job-per-tenant invariant | Defined already (FR-ONB-007): every retry resumes that same job instead of creating a second environment pipeline; the reverse lookup serves overview and reconcile reads |
| provisioning_jobs | `ix_provisioning_jobs_retry_queue` | `updated_at`, single-column, partial `WHERE status IN ('Pending', 'Failed')` | worker pickup "jobs awaiting retry, oldest transitioned-row first", in bounded batches (worker query, not a UI list) | Proposed, pending owner confirmation. R3: the awaiting-retry subset is tiny relative to all jobs. The key is `updated_at` rather than `status`, because what orders the pickup is FIFO position (oldest transitioned first), not the status label; the predicate carries the status filter. `updated_at` changes at every state transition, so a freshly failed job moves to the tail of the queue and is not retried instantly — a natural backoff floor without extra columns. Exact retry scheduling (delays, rate limits) is policy for the provisioning execution story; the predicate would be revised only together with a column the ERD does not yet have (see open items) |
| provisioning_job_attempts | `pk_provisioning_job_attempts` | `id` | single-row fetch by key | attempt fetch by id |
| provisioning_job_attempts | `uq_provisioning_job_attempts_attempt` | `provisioning_job_id, attempt_number`, unique, composite | "attempts of job X, in attempt order" and the numbering invariant | Defined already (FR-ONB-007): every attempt is recorded with its increasing number; the leftmost prefix doubles as the FK index; reconcile reads replay a job's history in order |
| platform_audit_records | `pk_platform_audit_records` | `id` | single-row fetch by key | audit row fetch by id |
| platform_audit_records | `ix_platform_audit_records_actor_platform_admin_id` | `actor_platform_admin_id`, single-column | audit rows of one actor | Proposed, pending owner confirmation. R1; also the by-actor inspection shape (FR-AUD-001's actor column) |
| platform_audit_records | `ix_platform_audit_records_recorded_at` | `recorded_at, id`, composite | the audit list's keyset paging order (newest first, section 4) | Proposed, pending owner confirmation. R3: audit is append-only, so no subset predicate exists; the composite indexes the displayed chronology exactly, and rows never update it so the write cost is the insert one |
| platform_audit_records | `ix_platform_audit_records_action_code` | `action_code, recorded_at`, composite | "filter by action code, newest first" | Proposed, pending owner confirmation. R4 exactly: equality filter column first, sort column last; audit inspection by action code is the core filtering axis of FR-AUD-001's action column, adopted with the first audit list implementation |
| platform_audit_records | `ix_platform_audit_records_subject` | `subject_type, subject_id, recorded_at`, composite | "history of one subject" | Proposed, pending owner confirmation. R4 exactly: the inspection view's target filter (FR-AUD-001's target column, FR-PLT-005's subject checks); pruned if the first implementation never filters by subject (R8) |

Deliberate omissions in the Master inventory, each with its reason:

- No index exists on `onboarding_requests.owner_password_hash` (R9): the column has no query and is excluded from any composite. The same holds for `platform_admins.password_hash` and, on the Tenant side, `tenant_users.password_hash`.
- `platform_payment_transactions.created_by` carries no foreign key in the ERD and no query, so it receives no index under R1/R6.
- No index on `tenants.lifecycle_status` alone: the tenant overview is a small administrative list (section 4) whose page depth stays small; a filtered variant that later earns an index is measured first (R8).
- No paging indexes on the small administrative lists that section 4 assigns offset paging (plans, tenants, onboarding detail, per-request payments): shallow pages over a small set are served by sequential reads; an index there would be write cost without a measured read problem. Each such list's index position is stated in section 4 and revisited only under R8.

## 3. Index list — Tenant database

The Tenant database is small and per-cafe: identity and role tables are tiny, and only the audit append grows without bound. The FK-rule (R1) choices follow the same conventions as the Master side.

| Table | Index or constraint name | Columns and kind | Query or rule served | Why |
|---|---|---|---|---|
| tenant_users | `pk_tenant_users` | `id` | single-row fetch by key | user fetch by id (staff detail reads) |
| tenant_users | `uq_tenant_users_normalized_user_name` | `normalized_user_name`, unique | sign-in lookup by normalized username, and the "one account per username" invariant | the Identity mechanism's contract; also the structural seed-idempotency guard: a duplicated owner account is structurally impossible on a provisioning retry (FR-ONB-007), because a re-run seed's normalized username collides with the already-seeded row instead of inserting a duplicate |
| tenant_users | `ix_tenant_users_normalized_email` | `normalized_email`, non-unique | staff lookup by normalized email (support/review flows) | Defined already (ERD): staff addresses are not identity-critical in the MVP, so the index is lookup support, not uniqueness |
| tenant_users | `ix_tenant_users_created_by` | `created_by` (self-FK), single-column | reverse lookup "accounts created by user X" (rare) | Proposed, pending owner confirmation. R1 as above |
| tenant_users | `ix_tenant_users_updated_by` | `updated_by` (self-FK), single-column | reverse lookup "edits attributed to user X" (rare) | Proposed, pending owner confirmation. R1 as above |
| tenant_users | `ix_tenant_users_deleted_by` | `deleted_by` (self-FK), single-column | reverse lookup "deactivations performed by user X" | Proposed, pending owner confirmation. R1 as above |
| tenant_roles | `pk_tenant_roles` | `id` | single-row fetch by key | role fetch by id |
| tenant_roles | `uq_tenant_roles_normalized_name` | `normalized_name`, unique | role lookup by name during authorization | the mechanism's normalized-uniqueness contract |
| tenant_roles | `ix_tenant_roles_created_by` | `created_by` (FK), single-column | reverse lookup "roles created by user X" (rare) | Proposed, pending owner confirmation. R1 as above |
| tenant_roles | `ix_tenant_roles_updated_by` | `updated_by` (FK), single-column | reverse lookup "edits attributed to user X" (rare) | Proposed, pending owner confirmation. R1 as above |
| tenant_user_roles | `pk_tenant_user_roles` | `id` | single-row fetch by key | membership fetch by id |
| tenant_user_roles | `uq_tenant_user_roles_user_role` | `tenant_user_id, role_id`, unique, composite | invariant "one membership per user-role pair"; leftmost prefix doubles as the FK index for `tenant_user_id` | duplicate memberships silently change authorization outcomes; the composite also answers "roles of user X" (the authorization read path) |
| tenant_user_roles | `ix_tenant_user_roles_role_id` | `role_id`, single-column | members of role X (staff screens, branch- or role-based views) | Proposed, pending owner confirmation. R1; the composite above only prefixes on `tenant_user_id` |
| tenant_user_claims | `pk_tenant_user_claims` | `id` | single-row fetch by key | claim fetch by id |
| tenant_user_claims | `ix_tenant_user_claims_tenant_user_id` | `tenant_user_id`, single-column | claims of user X | Proposed, pending owner confirmation. R1; read per user at authorization evaluation |
| tenant_role_claims | `pk_tenant_role_claims` | `id` | single-row fetch by key | claim fetch by id |
| tenant_role_claims | `ix_tenant_role_claims_role_id` | `role_id`, single-column | claims of role X | Proposed, pending owner confirmation. R1; read per role at authorization evaluation |
| branches | `pk_branches` | `id` | single-row fetch by key | branch fetch by id (branch detail reads) |
| branches | `ix_branches_created_by` | `created_by` (FK), single-column | reverse lookup "branches created by user X" (rare) | Proposed, pending owner confirmation. R1 as above |
| branches | `ix_branches_updated_by` | `updated_by` (FK), single-column | reverse lookup "edits attributed to user X" (rare) | Proposed, pending owner confirmation. R1 as above |
| cafe_settings | `pk_cafe_settings` | `id` | single-row fetch by key | profile-row fetch by id |
| cafe_settings | `uq_cafe_settings_singleton` | constant expression `(true)`, unique | the singleton invariant: at most one cafe profile row per database | Defined already (ERD, FR-CAF-001): the database boundary (one cafe per database) makes this a natural singleton, so the constraint enforces it structurally |
| cafe_settings | `ix_cafe_settings_created_by` | `created_by` (FK), single-column | reverse lookup (row is created by provisioning with NULL actor, so rarely exercised) | Proposed, pending owner confirmation. R1 as above |
| cafe_settings | `ix_cafe_settings_updated_by` | `updated_by` (FK), single-column | reverse lookup "edits attributed to user X" (rare) | Proposed, pending owner confirmation. R1 as above |
| tenant_audit_records | `pk_tenant_audit_records` | `id` | single-row fetch by key | audit row fetch by id |
| tenant_audit_records | `ix_tenant_audit_records_actor_user_id` | `actor_user_id`, single-column | audit rows of one actor | Proposed, pending owner confirmation. R1; also the by-actor inspection view (FR-IAM-005, FR-AUD-001); owner-vs-staff is derived through the join to `tenant_users`, not through this index |
| tenant_audit_records | `ix_tenant_audit_records_recorded_at` | `recorded_at, id`, composite | the tenant audit list's keyset paging order (newest first, section 4) | Proposed, pending owner confirmation. R3: the only tenant list that grows without bound; rows never update the key, so the write cost is the insert one |
| tenant_audit_records | `ix_tenant_audit_records_action_code` | `action_code, recorded_at`, composite | "filter by action code, newest first" | Proposed, pending owner confirmation. R4 exactly; the evaluation axis for the audit filter variants |
| tenant_audit_records | `ix_tenant_audit_records_subject` | `subject_type, subject_id, recorded_at`, composite | "history of one subject" | Proposed, pending owner confirmation. R4 exactly; pruned if the first implementation never filters by subject (R8) |

Deliberate omission on the Tenant side: no index on any password-hash column (`tenant_users.password_hash`, and the Master-side `owner_password_hash`) — no composite covers one (R9).

## 4. Pagination decisions per list

The pagination request contract (page-size shape, cursor encoding, endpoint semantics) is a later story. This section records the decision and the supporting index for each list the merged ERDs imply, so the later story implements decisions instead of inventing them.

### 4.1 Shared rules

All rules in 4.1 are **Proposed, pending owner confirmation**.

**Style split.** Each list is assigned exactly one paging style:

- **Keyset (cursor) paging** for lists that grow without bound or are append-heavy: audit lists (rows are preserved indefinitely under the confirmed logical-deletion policy, and their retention policy stays PRD-undecided, TBD-20), payment transactions (every attempt persists, FR-SUB-002), and the future operational streams (orders, stock movements) — lists whose total page depth is unbounded.
- **Offset paging, with an optional total count**, for small administrative lists where pages stay shallow and an exact total is useful UI information: tenants, plans, onboarding review queue, branches, staff users.

**Why.** Offset paging degrades with page depth: to return the Nth page the database must walk and discard `offset + limit` rows every time, so the cost is linear in depth even for a fixed page size. Keyset paging resumes at the previous page's last key, so every page — including deep ones — scans only the page width once. Small administrative lists stay shallow by construction (a cafe's branch or staff list never approaches a degraded depth), so offset costs nothing there and gains the total-count convenience. Page size: default **20**, maximum **100**, enforced in the later pagination contract; a cursor or a page above the maximum is a request-contract error, not a silent clamp.

**Rejected alternative.** Offset paging everywhere: every deep page (exports, "load more" chains, aged data in audit and payment history) re-pays the discarded-prefix cost; a UI that walks the tail of an audit history degrades exactly because that is where the data is.

**Deterministic sort.** Every list states its full `ORDER BY` explicitly, ending with the `id` column as the tie-break, so two pages can never disagree about row position. A keyset cursor carries the sorted key values plus the `id` of the last row of the previous page. The sort key must be **immutable per row**: a cursor is only valid where the rows it passed keep their positions — an `updated_at`-ordered cursor skips or duplicates a row whose `updated_at` changes mid-walk; `created_at`/`recorded_at` and identity numbers are the eligible keys.

**Why.** A non-deterministic sort makes pagination undefined; a mutable sort key makes a cursor that "looked right" hand back a shifted window. Tying the tie-break to a UUIDv7 primary key keeps the cursor semantically cheap and lets the engine land directly on the row's index entry instead of a range approximation.

**Rejected alternative.** An `updated_at`-sorted cursor for operational monitoring (jobs, payments): the monitoring display feels natural until a row it already passed is retried or re-settled, and the window silently shifts. Monitoring queries that need a live view re-read from the head instead of paging a stable walk.

**Count semantics.** `COUNT(*)` is optional. It is included where the UI shows an exact total (administrative lists: "42 tenants"), omitted where the UI only loads the next page. A keyset list never reports a count unless a specific UI shape requires it — the cost is a full re-scan of the filter, and an exact number is stale by the time it renders on a list that accepts writes.

**Rejected alternative.** A mandatory count on every list: it taxes every page request with a second scan and hides the fact that the number cannot be stable on active tables.

### 4.2 Keyset serving variants

Two declared serving shapes cover keyset lists:

- **K1 — declared column keyset.** `ORDER BY <created/recorded column> DESC, id DESC`, served by a declared composite `ix_<table>_<created-or-recorded-column>` from sections 2/3. Used where the list's displayed order is the exact time-column order (audit, payments).
- **K2 — primary-key keyset.** `ORDER BY id DESC` (or ASC), served by the primary-key index alone, no additional index. The conventions establish that primary keys are application-generated UUIDv7 values whose keys keep roughly time-ordered index locality, so a PK-order walk reads as "newest first" or "oldest first" for browsing purposes without exact cross-writer chronology. Used where a support/monitoring list wants recency and does not promise exact time order (onboarding request history, provisioning job history).

**Why K2 exists.** It carries zero additional write cost (R7), because the primary-key index (already present for the every-row fetch path) serves it; the trade-off is approximate cross-writer chronology, which is acceptable exactly where the list does not promise exact time order.

**Rejected alternative.** Making K1 universal: exact time ordering is often not the product requirement for a history list, and a second composite for a query the primary key already serves is a direct R6/R8 violation.

### 4.3 Master lists

| List | Style | Deterministic sort | Supporting index | Why |
|---|---|---|---|---|
| Platform tenant overview (FR-PLT-005) | offset + optional count | `created_at DESC, id DESC` | none at MVP scale (sequential read; revisit under R8) | small administrative list: the row count is bounded by the number of cafes |
| Plans catalog (FR-SUB-001) | offset + optional count | `code ASC, id ASC` | `uq_plans_code` | tiny catalog; `code` is stable and selectable-filtering stays an application predicate; reuses the uniqueness index rather than adding one (R6) |
| Plan versions of one plan (detail list) | offset (small) | `version_number DESC` (composite `(plan_id, version_number)` already unique, so the `id` tie-break is vacuous for any plan filter) | `uq_plan_versions_plan_version` | filter and sort share one composite; the ERD constraint serves the list, so no extra index (R6) |
| Onboarding review queue | offset + optional count | `created_at ASC, id ASC` | `ix_onboarding_requests_review_queue` (Proposed, section 2) | the pending subset is small and bounded, so offset depth cannot degrade; queue order is "oldest pending first"; the UI can show an exact queue total cheaply |
| Onboarding requests history (all statuses) | keyset (K2) | `id DESC` | `pk_onboarding_requests` | grows with every registration and every row is kept under the logical-deletion policy; the list serves recency browsing, not exact-chronology display; zero added index cost |
| Payment transactions | keyset (K1) | `created_at DESC, id DESC` | `ix_platform_payment_transactions_created_at` (Proposed) | unbounded financial history; displayed chronology is exact and the access shape is append-only writes (R7 stays low) |
| Payments of one onboarding request (detail) | offset (tiny) | `created_at ASC, id ASC` | `ix_platform_payment_transactions_onboarding_request_id` (Proposed) | the retry history of one request is short (FR-ONB-004); the FK index already serves the filter, so the page adds no new index |
| Provisioning jobs history | keyset (K2) | `id DESC` | `pk_provisioning_jobs` | jobs are an append-heavy keyset list: every retry rewrites the row and its attempts keep arriving; the monitoring display reads newest first without promising cross-writer time order; the cursor anchor is the immutable id (an `updated_at` cursor would wobble under retries) |
| Jobs awaiting retry (worker pickup, not a UI list) | fixed batch, no paging | `updated_at ASC` | `ix_provisioning_jobs_retry_queue` (Proposed) | a worker consumes the live subset in bounded batches; retry semantics themselves stay with the provisioning execution story (open item) |
| Subscription periods of one subscription | keyset | `period_number ASC` (composite `(subscription_id, period_number)` is unique per subscription, so no further tie-break) | `uq_subscription_periods_period_number` | numbered per-subscription history carries an immutable cursor — billing cycles arrive, possibly over years, so the list must not degrade as the type grows |
| Renewal requests of one subscription | offset (small) | `requested_at DESC, id DESC` | none at MVP scale (per-subscription arithmetic is tiny; revisit under R8) | few renewal cycles per cafe per year — the list stays shallow |
| Platform audit (FR-AUD-001, FR-PLT-005) | keyset (K1) | `recorded_at DESC, id DESC` | `ix_platform_audit_records_recorded_at` (Proposed) | the platform list that grows fastest and is never pruned (retention stays PRD-undecided, TBD-20); exact chronology is the displayed column |
| Audit filtered by action code / actor / subject | keyset (K1) | `recorded_at DESC, id DESC` under the filter | `ix_platform_audit_records_action_code` / `ix_platform_audit_records_actor_platform_admin_id` / `ix_platform_audit_records_subject` (Proposed; subject variant pruned if unused under R8) | the same list family with fixed filters; each filter's composite puts the equality column first (R4) |
| Renewal payments of one renewal request | not paged (small read) | none; read by FK | `ix_platform_payment_transactions_renewal_request_id` (Proposed) | one approved renewal yields a short payment trace (FR-SUB-002) — a detail read, not a list |

### 4.4 Tenant lists

| List | Style | Deterministic sort | Supporting index | Why |
|---|---|---|---|---|
| Branches (FR-CAF-002) | offset + optional count | `name ASC, id ASC` | none at MVP scale (sequential read; revisit under R8) | small administrative list inside one cafe |
| Staff users (FR-IAM-003) | offset + optional count | `created_at DESC, id DESC` | none at MVP scale (sequential read; revisit under R8) | small administrative list per cafe; deactivated accounts remain app-predicate filtering per the logical-deletion policy, not index predicates |
| Tenant audit (FR-IAM-005, FR-AUD-001) | keyset (K1) | `recorded_at DESC, id DESC` | `ix_tenant_audit_records_recorded_at` (Proposed) | the one tenant list that grows without bound; exact chronology is the displayed column |
| Tenant audit filtered by action code / actor / subject | keyset (K1) | `recorded_at DESC, id DESC` under the filter | `ix_tenant_audit_records_action_code` / `ix_tenant_audit_records_actor_user_id` / `ix_tenant_audit_records_subject` (Proposed; subject variant pruned if unused under R8) | same list family with fixed filters |
| Roles catalog / claims / entitlement reads | not paged (full reads, tiny) | none | the section 3 constraint/index entries | authorization-evaluation reads the whole small set per user or role |

### 4.5 Future operational lists

Orders, stock movements, and the other operational streams that later stories introduce follow the shape documented here rather than being retrofitted later: they are append-heavy, grow without bound, and are the reason the keyset rule for operational lists (and its K1/K2 variants) exists now. Their per-list rows (the sort column choice between each table's creation timestamp and its business event column, and the declared supporting composite) are recorded with each list's own story, against this section's rules.

## 5. How to add an index (checklist)

1. Name the query or the invariant the index serves — no query, no index (R6).
2. Look for an existing leftmost prefix, backward-scan plan, or (unique/partial) constraint that already serves it — reuse before adding anything (R4, R8).
3. If a second candidate for the same query exists, measure both with `EXPLAIN (ANALYZE)` on representative data; without a measured win, do not add (R8).
4. Name it per R5 and add or reconcile the row for the table in section 2 or 3 of this document, including the serving query and the why.
5. Budget the write cost (R7): prefer a partial predicate bound to the consumed subset (R3) over a full composite when the subset is what queries read.

## 6. Open items

The following items are decisions intentionally left open: this document neither resolves them nor assumes them away.

- **Retry scheduling policy for provisioning jobs** (the executor story's policy): whether retries need a persisted "next-attempt time" column so the queue's FIFO can delay failing jobs by policy instead of position is undecided. The ERD has no such column and this document adds none; the current partial index above serves only the position-ordered FIFO. If a column is later approved, its index predicate is revised in this document and implemented in a new index-design cycle — never invented silently at mapping time.
- **Pruning candidates among the actor FK indexes.** The `created_by`/`updated_by`/`deleted_by` self-referencing references on identity tables (and the matching columns on small registry tables) keep their FK-rule (R1) indexes for uniformity and the rare Support reverse lookup. If the owner prefers strict query-driven indexing (R6) even as a matter of course, the list below names the candidates the review may prune: `ix_platform_admins_created_by`, `ix_platform_admins_updated_by`, `ix_platform_admins_deleted_by`, `ix_platform_admin_roles_created_by`, `ix_platform_admin_roles_updated_by`, `ix_plans_created_by`, `ix_plans_updated_by`, `ix_plan_versions_created_by`, `ix_plan_versions_updated_by`, `ix_tenants_created_by`, `ix_tenants_updated_by`, `ix_tenant_databases_created_by`, `ix_tenant_databases_updated_by`, `ix_subscriptions_created_by`, `ix_subscriptions_updated_by`, `ix_subscription_periods_created_by`, `ix_subscription_periods_updated_by`, `ix_subscription_renewal_requests_created_by`, `ix_subscription_renewal_requests_updated_by`, `ix_platform_payment_transactions_updated_by`, `ix_tenant_users_created_by`, `ix_tenant_users_updated_by`, `ix_tenant_users_deleted_by`, `ix_tenant_roles_created_by`, `ix_tenant_roles_updated_by`, `ix_branches_created_by`, `ix_branches_updated_by`, `ix_cafe_settings_created_by`, `ix_cafe_settings_updated_by`. Default state is kept, and a prune is a recorded owner decision, not a drop during implementation.
- **Subject-filter audit composites** (`ix_platform_audit_records_subject`, `ix_tenant_audit_records_subject`) are proposed under the assumption that by-subject inspection happens (FR-AUD-001's target column, FR-PLT-005's inspection); if the first audit implementation ships without it, the index is pruned rather than kept speculative (R8).
- **Audit retention and tamper resistance** (PRD TBD-20) stays PRD-undecided: it can change what retention means for audit list depth and for the indexes that serve archived rows; this document deliberately does not rule on it.
- **Plan catalog pricing and simultaneity of billing-period offerings** (PRD TBD-01) and the **subscription lifecycle policy** (PRD TBD-04) — neither is decided here; any constraint these decisions produce later would add a conditional unique index through its own reviewed story.

## Decision log

| Topic | Decision | Status |
|---|---|---|
| Indexing rules R1–R9 | every FK indexed unless covered by an existing leftmost prefix; invariants carried by unique constraints; partial indexes for live subsets; composite keys filter-then-sort; conventions-based naming; no index without a named query or invariant; write-amplification budgeting; `EXPLAIN` before stacking; never index a password hash or include one in a composite | stated by this story's acceptance criteria (not new decisions of this document) |
| Master and Tenant index inventory | the per-table rows in sections 2 and 3, restatements of the merged ERDs plus the Proposed additions | Proposed, pending owner confirmation (each row marked) |
| Pagination style split | keyset for unbounded/append-heavy lists, offset + optional count for small administrative lists; default size 20, maximum 100 | Proposed, pending owner confirmation (stated by the story) |
| Keyset serving variants | K1 declared column-keyset composites for exact-time lists, K2 primary-key (UUIDv7) cursors for recency lists | Proposed, pending owner confirmation |
| Per-list decisions and supporting indexes | section 4.3 and section 4.4 | Proposed, pending owner confirmation (each row marked) |
| Out of scope here | the database views strategy (`CFMD-256`), the pagination request contract, entity or migration code | tracked separately |
