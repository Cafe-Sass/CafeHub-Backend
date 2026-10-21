# Database Conventions

One agreed set of database conventions that every later CafeHub entity configuration and migration follows. Companion documents: `architecture/database-schema-decisions.md` (scope, isolation, placement, migration arrangement) and `architecture/architecture-decisions.md`. Topics marked **Proposed** await owner confirmation; topics marked **Confirmed** restate decisions recorded in an existing document.

## Naming

**Convention (Proposed, pending owner confirmation).** SQL identifiers are `snake_case`: plural table names (`tenants`, `cafe_profile`), primary key `id`, foreign keys `<singular entity>_id` (for example `tenant_id`, `plan_version_id`). Constraint and index names use fixed prefixes: `pk_`, `fk_`, `uq_`, `ix_`, `ck_`. Check constraints follow `ck_<table>_<rule>` (for example `ck_onboarding_requests_status`). Qualifiers stay short so names remain inside PostgreSQL's 63-character identifier limit.

EF Core maps these names through **explicit configuration only**: one `ToTable(...)` per entity, explicit column names, `HasDatabaseName` for indexes, `HasConstraintName` for primary key, foreign key, and unique constraints, and check constraints declared on the table builder (`ToTable(t => t.HasCheckConstraint("ck_...", "..."))`). No naming-convention package is used. Adding one (an automatic CamelCase to `snake_case` rewrite) requires approval through the dependency policy.

**Why.** PostgreSQL folds unquoted identifiers to lowercase, so mixed-case names force quoting in every hand-written query, log line, and migration snippet. `snake_case` matches the PostgreSQL ecosystem and keeps generated SQL readable; the fixed prefixes make object types unambiguous in database logs and migration reviews. Explicit per-entity configuration keeps the naming rule visible in the model itself, free of a third-party renaming heuristic that could silently diverge from this document.

**Rejected alternative.** A naming-convention package: the rename happens behind a convention transform, so the model snapshot no longer states real names and review cannot check them; it also adds a package dependency outside the approved list. Quoted PascalCase SQL identifiers everywhere was also rejected.

## Identifiers

**Convention (Proposed, pending owner confirmation).** Primary keys are `uuid` columns named `id`. The application generates time-ordered UUIDv7 values with `Guid.CreateVersion7()` (available since .NET 9) and assigns them before dispatching a command to persistence; the database stores the value instead of creating it with a default.

**Why.** UUIDv7 keeps index keys roughly time-ordered, avoiding the B-tree fragmentation of random UUIDv4 keys, while never leaking record counts or insertion position the way a plain integer sequence does. Generating the value in the application keeps it usable before any database round trip: it appears in command results, correlation logs, idempotency records, and unit tests with hand-written fakes, which matters because the repository test policy forbids in-memory and SQLite database providers.

**Rejected alternative.** Sequential integer keys (expose row counts and ordering estimates, and merge awkwardly across the Master and Tenant databases in later reporting); random UUIDv4 (poor index locality); database-generated values such as `gen_random_uuid()` (the identifier exists only after the insert, so the application cannot reference it during the same use case without an extra round trip).

## Timestamps

**Convention (Proposed, pending owner confirmation).** Timestamp columns are `timestamptz`, always holding a UTC instant, and map to `DateTimeOffset` in C#. Time is injected as `TimeProvider` and read through `GetUtcNow()`; production code never calls `DateTime.Now`, `DateTime.UtcNow`, or `.Now`/`.UtcNow` on a `DateTimeOffset`.

**Why.** `timestamptz` is the only PostgreSQL timestamp type with correct instant semantics; `timestamp` without time zone drops the offset and makes historic records ambiguous after zone or policy changes. `DateTimeOffset` in code removes any doubt about the value's source. An injected `TimeProvider` keeps tests deterministic with hand-written fakes and keeps CI behavior independent of the machine time zone.

**Rejected alternative.** `timestamp` without time zone columns; plain `DateTime` mapping (loses the offset); ambient static clocks (untestable without a mocking library, which the test policy forbids); NodaTime (a dependency outside the approved list; `DateTimeOffset` covers the MVP need).

## Enums

**Convention (Proposed, pending owner confirmation).** Enum-like states are stored as `text` with a named `CHECK` constraint listing the allowed labels (for example `ck_tenant_lifecycle_status CHECK (lifecycle_status IN ('Pending', 'Active', 'Suspended', ...))`). Labels match the C# enum constant names exactly.

**Why.** Readable rows make database inspection, support, and debugging direct. The `CHECK` constraint is a plain, cheap SQL object: changing the allowed set is an ordinary migration without type-object surgery. The constraint also blocks invalid values written outside application paths.

**Rejected alternative.** Integer codes (opaque in rows, logs, and support queries, and offset mapping must be tracked forever); native PostgreSQL enum types (the provider's own documentation records the ALTER limitation: values cannot be removed and renames are not automatic, so cleanup requires raw SQL against the type).

## Money

**Convention (Proposed, pending owner confirmation).** Monetary amounts are `numeric(12,2)` columns mapped to `decimal`, each accompanied by a lowercase currency code column (`currency_code text`); a value never travels without its unit. Currency and tax rules the PRD leaves open stay open: plan content and prices remain PRD TBD-01, the payment provider remains TBD-02, and Tax Authority integration remains TBD-14.

**Why.** `numeric` is exact with a fixed scale; 12 digits with 2 places covers foreseeable cafe-business amounts without over- or under-scaling. A separate currency code column keeps unit as data, so an amount is unambiguous whenever more than one currency can exist.

**Rejected alternative.** `real`/`double precision` (binary floating point rounds differently from decimal settlement semantics); the PostgreSQL `money` type (its rendering and behavior depend on database locale settings, so identical values behave differently per deployment); integer minor units (minor-unit scale varies by currency, and hiding that scale inside the column invites conversion defects); `numeric(19,4)` with the currency implied (unit confusion).

## Deletion

**Convention (Confirmed — PRD section 9, rule 23).** Ordinary application delete operations never physically delete persistent business records. Removal is expressed as **logical deletion**: through the entity's own lifecycle status, retirement or selection flags, deactivation, or an appropriate status column of the same style. Where an entity has no natural status column, a generic soft-delete marker is used: `deleted_at timestamptz NULL` plus `deleted_by` where an acting identity exists (naming per conventions; Proposed in detail). The marker is never a boolean and is never mechanically added to every table. Queries and authorization treat soft-deleted rows as absent: an application must never consider a deactivated actor authorized (FR-IAM-002 style checks) and read paths filter or exclude the marker at their first query layer.

Immutable financial and audit history is never removed through ordinary application workflows: subscription periods, payment transactions, provisioning attempts, and append-only audit records are never physically deleted, and audit rows are additionally never updated after insertion. Retention and tamper-resistance for audit storage remain PRD TBD-20.

Physical deletion of stored media such as images and videos is the explicitly approved exception, subject to the applicable storage-cleanup policy (PRD section 9, rule 23).

**Why.** The PRD requires it: logical deletion preserves historical records, keeps audit durability, and prevents support queries or accidental user actions from destroying subscription, payment, and auditing history that the business reconstructs later. Status-based removal is preferred over a marker because it describes what the row is now (`Active`, `Suspended`, `Terminated`, an `is_selectable` flag) and keeps the row referentially intact, including its foreign keys, without resurrection logic. Every foreign key to a soft-deleted row stays valid precisely because a row is never physically removed (for example audit records referencing a deactivated platform administrator keep their `actor_platform_admin_id` FK).

**Identity specifics (Confirmed, owner decision: deactivation, not physical deletion).** Platform admin user rows are deactivated, never physically deleted, so audit references keep their foreign key and authorization must treat a deactivated admin as unauthorized; credential machinery must not keep a previously valid credential authoritative for a deactivated account. Identity **role-membership and claim assignment rows** are the deliberate exception: they are current assignment state rather than history — removing one restores the authorization state directly and there is no hidden lifecycle status that could silently keep a removed membership active — so they may be physically removed by approved administration workflows, and each removal is audited. Role and claim rows themselves stay until their last assignment is gone. (The `deleted_at`/`deleted_by` marker naming and the precise administrative workflow for join-row removal are Proposed refinements of the confirmed policy.)

**Rejected alternative.** Physical (`DELETE`) removal of business records for ordinary deletes (violates PRD section 9, rule 23 and silently breaks historical reconstruction); a uniform `is_deleted` boolean on every table with no timing (no audit context and no integrity for retention semantics); soft-delete markers on join/assignment rows (a hidden removal state that can leave a disabled user still authorized or a removed membership still active).

## Concurrency

**Convention (Proposed, pending owner confirmation).** Optimistic concurrency uses the PostgreSQL `xmin` system column through the Npgsql provider row-version support. Verification basis for the installed `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3`: the provider documentation (Concurrency Tokens) shows the supported mapping of a `uint` property as row version using either the `[Timestamp]` data annotation or the standard EF Core Fluent API `.IsRowVersion()`, mapping that property to the `xmin` system column, and documents that serialization failures (`40001`) and lock deadlocks (`40P01`) during `SaveChanges` surface as `DbUpdateConcurrencyException`. Those are the current APIs used from the provider documentation at the time of writing; CFMD-74 applies the Fluent form at entity configuration time.

**Why.** PostgreSQL has no server-side auto-updating column comparable to SQL Server `rowversion`, but every heap table carries `xmin` for free; it is bumped automatically on every update, so a lost-update guard costs zero schema surface and does not depend on an additional application-managed column. Failures arrive as `DbUpdateConcurrencyException`, a typed signal instead of silent overwrites.

**Rejected alternative.** Application-managed version counters or `updated_at` equality checks (a manual protocol every writer must remember; the tracked column is itself writable, so the guard can be bypassed); preemptive pessimistic locking for general mutations (over-provisioned for MVP load and converts correctness into deadlock-management work).

## Audit columns

**Convention (Proposed, pending owner confirmation).** Every mutable business table carries `created_at timestamptz` and `updated_at timestamptz`. Where a formal acting principal exists (Super Admin on the Master database; owner or staff on a Tenant database), the table also carries `created_by` and `updated_by` referencing the actor identity table **inside the same database**. Writes made before any identity exists (for example a public onboarding request) leave those columns `NULL` and carry the actor context in the relevant audit record instead. Append-only audit tables drop `updated_at`/`updated_by` by design.

**Why.** The PRD requires sensitive actions to be attributable to the authenticated actor with timestamps (PRD FR-IAM-005, FR-AUD-001). Actor references stay inside one database because cross-database keys are forbidden (see Cross-database references). Requiring the columns only where an actor exists keeps the public registration flow honest: inventing a principal value would be worse than an honest `NULL` plus the event-level audit record.

**Rejected alternative.** Free-text actor names (unverifiable, easily spoofed, no referential integrity); actor foreign keys reaching into the other database (forbidden); mandatory `created_by` on every table including pre-identity public data (fabricates a principal that does not exist).

## Case-insensitive uniqueness

**Convention (Proposed, pending owner confirmation).** Case-insensitive uniqueness is enforced **by the database**, never only by application validation, for emails, slugs, and other case-insensitive identity text. Three declared mechanisms cover all cases:

- When the value is defined to be stored already lowered (tenant slugs), the column carries a `CHECK` constraint enforcing that the stored value equals `lower(value)`, and a plain unique index (`uq_...`) provides uniqueness. Validation lowercases before writing; the CHECK is the enforcement backstop.
- When the stored value must keep its original case (an owner email typed at registration) and the identity mechanism supplies no normalized companion column, a unique expression index on `lower(column)` is created with raw SQL inside the migration, together with the matching drop statement so the history stays reversible. EF Core models indexes only over columns and the Npgsql provider documents no first-class expression-index feature on its indexes documentation page, so the statement is intentionally hand-written and reviewed.
- When the chosen identity mechanism (the confirmed default is ASP.NET Core Identity with explicitly customized GUID-keyed entities; see `architecture/erd-master.md`) supplies its own normalized columns (`normalized_user_name`, `normalized_email`), uniqueness uses a plain unique index on those normalized columns exactly as the mechanism is designed to be used.

**Why.** PostgreSQL text comparison is case-sensitive, so a plain unique index on `email` would let `W@x.com` and `w@x.com` coexist as two identities. Columns guaranteed lowercase get a plain index without duplication; mixed-case values get an expression index. Both keep the guarantee at the data boundary.

**Rejected alternative.** The `citext` extension: it requires `CREATE EXTENSION`, adds an extension dependency, does not cover accents and other collation aspects the PostgreSQL documentation lists, under-supports some text functions, and does not allow mixing case-sensitive and case-insensitive comparison of the same column in one query. Also rejected: column-level non-deterministic ICU case-insensitive collations, which per the PostgreSQL documentation restrict `LIKE` pattern matching on such columns and couple every future query to collation state; an indexed generated column mirroring `lower(value)` remains an option, but adds schema surface beyond either declared mechanism.

## JSON columns

**Convention (Proposed, pending owner confirmation).** `jsonb` appears only where the stored object is genuinely flexible and is never filtered on, joined on, aggregated, or constrained by the database. Anything a query path, a `CHECK` rule, an index, or a workflow depends on is a real column instead. When a `jsonb` column is needed, EF Core's native JSON column mapping (stored as `jsonb` on Npgsql) is the mechanism, and the schema is defined by the entity type, not by loose SQL at write time.

**Why.** `jsonb` is a flexibility tool, not a way to hide schema. Moving business meaning into JSON drops the type system, foreign-key integrity, `CHECK` validation, index precision, migration visibility, and ERD expressiveness for exactly the facts CafeHub's modules depend on.

**Rejected alternative.** The `json` type (stored as text; comparison and validation semantics weaker than `jsonb` for identical use cases); half-modeling a business area inside `jsonb` and promoting fields to real columns later (creates permanent two-source-of-truth pressure).

## Text

**Convention (Proposed, pending owner confirmation).** Text columns are `text`, with named `CHECK` constraints where the business has a hard length limit (for example a slug or code-length bound). `varchar(n)` is rejected; validation exists at two levels: the application validator and the `CHECK` constraint.

**Why.** In PostgreSQL, `varchar(n)` and `text` behave identically with identical storage, so the `n` adds no benefit; the limit belongs in a named, ERD-visible constraint that can also express rules across columns. Keeping both layers means the database still rejects an invalid length even when application validation drifts out of sync with it.

**Rejected alternative.** `varchar(n)` chosen defensively (no advantage over `text` plus a `CHECK` rule); unbounded text without a `CHECK` constraint (rows are unrestricted at the data boundary).

## Cross-database references

**Convention (Confirmed).** No cross-database foreign keys, joins, or triggers exist. The Master database refers to a tenant only by identifier and by **secret reference**, never a raw connection string; the reference is the secret reference key used in the established `TenantConnections:<SecretReference>` setting convention (`src/CafeHub.Infrastructure` `ConfigurationKeys.cs`). A Tenant database never carries a foreign key to a Master row; tenant tables carry no tenant discriminator column because each cafe has its own database (`architecture/database-schema-decisions.md`).

**Why.** PostgreSQL cannot create foreign keys across separate databases, and modeling the boundary through stored identifiers keeps each database independently validatable and backup-able. Secret references instead of connection strings keep secret values out of normal rows and readable only at connection-resolution time (PRD sections 10.2 and 11.1).

**Rejected alternative.** Duplicating platform-owned data inside tenant databases (a duplicate that drifts, cannot be repaired atomically, and invites cross-database joins); cross-boundary shared rows are not designed — integrations across the Master/Tenant boundary use identifiers (`architecture/module-conventions.md`).

## Schemas

**Convention (Proposed, pending owner confirmation).** Exactly one schema, `public`, per database, for both the Master database and every Tenant database.

**Why.** Each database is small and single-purpose, and module boundaries in this codebase are Application-layer boundaries rather than database namespaces. A second schema would add `search_path`, permissions, and tooling considerations for zero MVP benefit and is easy to introduce later if module data is genuinely separated.

**Rejected alternative.** Schema-per-module on day one (unmeasured benefit, pre-imposed permissions and tooling complexity).

## Migrations

**Convention (Confirmed).** Each context has exactly one migration history (`src/CafeHub.Infrastructure/Persistence/Master/Migrations` and `.../Tenant/Migrations`), as already recorded in `architecture/database-schema-decisions.md`. Applied (merged) migrations are never edited; problems are fixed with a new migration. Hand-written raw SQL appears only where required (for example an expression index), with both the up and down statements in the same migration.

**Why.** Two contexts, two databases: one history per context keeps each database's history independently correct and avoids order mixing. Editing an applied migration would rewrite what deployed databases already executed, which the recorded history exists to prevent.

**Rejected alternative.** One shared migration history for both contexts (forces ordering coupling and contradicts the per-context arrangement already recorded).

## Decision log

| Topic | Decision | Status | PRD ID |
|---|---|---|---|
| Naming | `snake_case` identifiers; plural tables; `id`, `<entity>_id`; `pk_`/`fk_`/`uq_`/`ix_`/`ck_` prefixes; explicit EF Core configuration without a naming package | Proposed | — |
| Identifiers | Application-generated UUIDv7 primary keys, `uuid` columns | Proposed | — |
| Timestamps | `timestamptz` in UTC mapped to `DateTimeOffset`, sourced from injected `TimeProvider` | Proposed | — |
| Enums | `text` with `CHECK`-constrained label lists | Proposed | — |
| Money | `numeric(12,2)` plus a currency code column; pricing/tax stay TBD | Proposed | TBD-01, TBD-02, TBD-14 |
| Deletion | Logical deletion by default (lifecycle status, retirement flags, `deleted_at` marker only where no natural status exists); immutable financial and audit history never physically removed; stored-media deletion the only approved exception; Identity users deactivated, join rows the documented exception | Confirmed (PRD section 9, rule 23); marker naming Proposed | FR-AUD-001, TBD-20 |
| Concurrency | Optimistic concurrency on PostgreSQL `xmin` via the Npgsql row-version mapping | Proposed | — |
| Audit columns | `created_at`, `updated_at`; `created_by`, `updated_by` where an actor exists | Proposed | FR-IAM-005, FR-AUD-001 |
| Identity mechanism | ASP.NET Core Identity with explicitly customized GUID-keyed entities for platform admin identity | Confirmed (owner decision) | — |
| Case-insensitive uniqueness | Database-enforced; lowered storage plus `CHECK` and a plain unique index, or a `lower(column)` expression index where mixed case is stored | Proposed | FR-ONB-004 |
| JSON columns | `jsonb` only for flexible, never-filtered metadata | Proposed | — |
| Text | `text` plus named `CHECK` length rules; no `varchar(n)` | Proposed | — |
| Cross-database references | None; identifier and secret reference only across the Master/Tenant boundary | Confirmed | FR-PLT-001, FR-PLT-002 |
| Schemas | One `public` schema per database | Proposed | — |
| Migrations | One history per context; merged migrations are never edited | Confirmed | — |
