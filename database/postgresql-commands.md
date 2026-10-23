# PostgreSQL Operations Commands

Operator-level commands for creating the CafeHub Master database and tenant database layout with least-privilege PostgreSQL roles, plus the manual verification script that proves database-level tenant isolation before a release. Companion documents: `architecture/database-schema-decisions.md` (the two-database arrangement and fail-closed isolation), `architecture/database-conventions.md` (naming and review conventions), `architecture/erd-master.md` and `architecture/erd-tenant.md` (the tables each database owns), `architecture/architecture-decisions.md` (repository decisions and the isolation risk acceptance), `architecture/indexing-and-pagination-strategy.md`, and `architecture/database-views-strategy.md`.

Requirement identifiers cited in this document exist in the PRD and were verified there before use; the PRD text itself stays untracked. This sub-task is documentation only: no application code, entity, migration, DbContext, test, package, or CI change is made.

**Placeholder-only rule.** Every value below is an unmistakable placeholder (`<host>`, `<port>`, `<tenant-slug>`, `<provisioning-role-password>`, `<master-app-password>`, `<tenant-app-password>`, and the like). No real host, username, password, connection string, or environment-specific credential ever appears in this file. Replace a placeholder only in a throwaway command shell or in the secrets store — never re-add a replacement value to any tracked file.

Two kinds of content are distinguished throughout:

- **Stated by this story** — the requirements the story fixes: the provisioning pattern, the manual isolation checks, and the cleanup commands.
- **Proposed, pending owner confirmation** — new operator choices (supported version floor, database and role names) with a why and a rejected alternative. The complete proposal list belongs to this story's review.

**Verification record.** Every command in this file was executed end-to-end on 2026-10-23 against a disposable local PostgreSQL cluster created and destroyed for this verification: PostgreSQL 18.6 (x86_64-windows, compiled by msvc-19.44.35229), initialized with `initdb`, `scram-sha-256` password authentication, listening on localhost port 55257, no shared or production environment touched. Roles, databases, grants, revocations, default privileges, all isolation checks (C1–C5 plus the positive controls below), and the cleanup sequence ran exactly as documented; every check produced the expected result. After verification the databases and roles were dropped, the server was stopped, and the data directory was deleted — matching row X1 of the checklist. The disposable instance carried no real credentials and shared no data with any regular local instance.

## 1. Prerequisites, supported version, and naming conventions

Prerequisites (one line per item):

- A reachable PostgreSQL server you are authorized to administer — needed because both `CREATE ROLE` and `CREATE DATABASE` run on the server, not inside a database.
- `psql` (any version not older than the server major version) — the commands below are plain psql statements, and psql is the tool that runs them.
- Admin access to the cluster through an administrator role (often the install-time superuser) — role creation is a cluster-level privilege ordinary roles do not have; later sections show the restricted provisioning role doing everything else.
- A secret store or password manager approved for the environment — role passwords must not travel through command history, shell logs, or committed files.

**Supported PostgreSQL version (Proposed, pending owner confirmation):** maintain the Master and tenant databases on PostgreSQL 15 or later; this document was verified on 18.6.

- **Why.** PostgreSQL 15 changed public-schema default privileges (the difference is documented in section 9.1) and the tenant layout here is explicit about those grants; declaring a floor keeps operator behavior and privilege expectations consistent instead of silently depending on whichever engine version each environment happens to run.
- **Rejected alternative.** "Any supported upstream version": PostgreSQL 14 and earlier grant `CREATE` on the `public` schema and database `CONNECT` to `PUBLIC` by default, so the same scripts behave differently per version; a mandatory version floor avoids a least-privilege model that differs from the one being reviewed.

**Naming conventions (Proposed, pending owner confirmation):** all names are snake_case and collide-free across the cluster:

| Object | Name pattern | Example shape |
|---|---|---|
| Master database | `cafehub_master` | fixed name |
| Tenant database | `cafehub_tenant_<tenant-slug>` | slug lowercased with `-` replaced by `_` |
| Provisioning role | `cafehub_provisioning` | fixed name |
| Master owner/migration role | `cafehub_master_owner` | `<database-name>_owner` |
| Master application role | `cafehub_master_app` | `<database-name>_app` |
| Tenant owner/migration role | `cafehub_tenant_<tenant-slug>_owner` | `<database-name>_owner` |
| Tenant application role | `cafehub_tenant_<tenant-slug>_app` | `<database-name>_app` |

- **Why.** `snake_case` matches the repository's SQL identifier conventions and avoids the universal quoting burden of mixed-case or hyphenated names. The `_owner`/`_app` suffix pair states which role runs migrations and which role only serves application traffic, so a permission review can read intent from the name. Length is safe: the tenant slug is at most 40 characters (`^[a-z0-9][a-z0-9-]{1,38}[a-z0-9]$`, `architecture/erd-master.md`), so `cafehub_tenant_` + 40 + `_owner` stays within PostgreSQL's 63-character identifier limit.
- **The slug-to-name mapping is not injective.** Slugs `a-b` and `a_b` lowercased and hyphen-replaced produce the same key `a_b`. The database name is not the tenant's identity — the Master registry (`tenants`, `tenant_databases`) and the `TenantConnections:<SecretReference>` configuration convention own identity and connection resolution. Provisioning resolves a name collision at creation time by choosing a distinct suffix, and records the chosen final name in the registry; nothing else ever assumes the database name was derived mechanically from the slug.
- **Rejected alternative.** Keeping hyphens inside database and role names (`"cafehub-tenant-<tenant-slug>"`): the names then require double quotes in every script, tool, and connection string, which is exactly the quoting burden the repository conventions reject for SQL identifiers.

## 2. Provisioning role

One named role with `CREATEDB` and `CREATEROLE` performs tenant onboarding work: it creates each owner role, each database, and (through membership) is the only bootstrap credential the provisioning pipeline needs.

```sql
CREATE ROLE cafehub_provisioning LOGIN CREATEDB CREATEROLE
    PASSWORD :'provisioning_password';
```

- Needed because tenant provisioning must be able to create databases (`CREATEDB`) and create the role pairs (`CREATEROLE`) autonomously, without holding administrator rights it would never need.

```sql
GRANT cafehub_master_owner TO cafehub_provisioning;
```

- `CREATE DATABASE ... OWNER <role>` requires the executing role to be a member of the owning role — this grant is what lets the restricted provisioning role create databases owned by the per-database owner roles (the same pattern repeats per tenant: `GRANT cafehub_tenant_<tenant-slug>_owner TO cafehub_provisioning;`).

The command above runs from an administrator session supplying the password through a psql variable (`-v provisioning_password="<provisioning-role-password>"`), so the value itself never appears in the command line, the script file, or the transcript.

**Elevated-privilege risks, stated explicitly:** a role holding `CREATEDB` and `CREATEROLE` can mint databases and mint roles. On PostgreSQL 16+ a `CREATEROLE` role gets administration rights only over the roles it creates (the release-16 restriction on granting memberships it does not administer); on 15 and earlier it can additionally grant membership in other non-administrator roles, an escalation surface the proposed version floor avoids. It therefore must never hold `SUPERUSER`, never own business data, and never be used as the application's runtime identity. It lives only where the provisioning background workflow (FR-ONB-006, FR-ONB-007) can reach it.

**Safe secret storage:** the provisioning credential, and every role password, is set from the environment's approved secrets mechanism — User Secrets or environment variables locally (the repository's established configuration style), the production secrets store in deployed environments — and by reading the value into a shell variable at invocation time rather than typing it:

```shell
# Never write the password literal into this file, a command line, or shell history.
PROVISIONING_PASSWORD="<provisioning-role-password>"   # Value comes from the secret store, not source control.
psql "host=<host> port=<port> dbname=postgres user=<admin-role-name>" \
  -v on_error_stop=1 \
  -v provisioning_password="$PROVISIONING_PASSWORD" \
  -f create-provisioning-role.sql
```

- Needed because PostgreSQL identifies roles by name and password, so a secret that leaks into a transcript leaks the database; the variable indirection keeps the committed commands placeholder-only while operators run real values from the secret store.

**Rejected alternative.** Reusing the cluster administrator role for provisioning: the pipeline would then hold superuser rights it never needs, and every provisioning defect would become a cluster-wide incident instead of an over-provisioned role defect.

## 3. Master database role pair (owner/migration and restricted application)

The Master database gets exactly two logins: an owner role that runs migrations and seeds, and an application role that only reads and writes business rows.

```sql
CREATE ROLE cafehub_master_owner LOGIN PASSWORD :'master_owner_password';
CREATE ROLE cafehub_master_app   LOGIN PASSWORD :'master_app_password';
```

- Needed because one role owning the schema (migrations under `ConnectionStrings:Master` design-time tooling) and one role serving runtime traffic is the minimum split that makes `ALTER` grants structurally impossible for the application role.

```sql
GRANT cafehub_master_owner TO cafehub_provisioning;
```

- Needed before section 5 can create `cafehub_master` owned by `cafehub_master_owner` from the provisioning role's session (owner-membership rule of `CREATE DATABASE ... OWNER`).

**Why the boundary.** `architecture/database-conventions.md` fixes one EF migration history per context; the migration history in `src/CafeHub.Infrastructure/Persistence/Master/Migrations` runs as the owner role, while every runtime connection resolved through `ConnectionStrings:Master` uses the application role. If both ran as one role, EF tooling and an application defect would both hold DDL rights on the registry.

**Rejected alternative.** One shared role plus `SET ROLE` juggling at runtime: correctness would depend on every runtime path remembering to downgrade its privileges, which one missed code path silently breaks.

## 4. Tenant database role pair (owner/migration and restricted application)

Each tenant gets its own dedicated pair — two per tenant, named from that tenant's slug — even though the SQL is identical per tenant.

```sql
CREATE ROLE cafehub_tenant_<tenant-slug>_owner LOGIN PASSWORD :'tenant_owner_password';
CREATE ROLE cafehub_tenant_<tenant-slug>_app   LOGIN PASSWORD :'tenant_app_password';
GRANT cafehub_tenant_<tenant-slug>_owner TO cafehub_provisioning;
```

- Needed because tenant-grade isolation is structurally real only when each tenant's data is served by a role no other tenant holds: shared roles would merge the very boundary the layout exists to enforce (each cafe has its own database and its own login, FR-PLT-001).
- The membership grant is the same owner-membership requirement of `CREATE DATABASE ... OWNER` as in section 3, now applied to the tenant's owner role.

**Rejected alternative.** One shared tenant application role across all tenant databases: a single leaked credential or a bug in connection resolution would then grant cross-tenant access in one step, and no per-tenant revocation could contain it. Per-tenant roles are the database-level side of the fail-closed resolution design (`architecture/database-schema-decisions.md`).

## 5. Master database creation and permissions

Run in the provisioning role's session against the cluster maintenance database (`dbname=postgres`), one statement per psql `-c` because `CREATE DATABASE` cannot run inside a transaction block:

```sql
CREATE DATABASE cafehub_master OWNER cafehub_master_owner;
```

- Creates the platform registry database directly owned by the owner role that will apply its migrations.

All remaining Master commands run inside `cafehub_master`, in the owner role's session (`psql "host=<host> port=<port> dbname=cafehub_master user=cafehub_master_owner"`):

```sql
REVOKE ALL ON DATABASE cafehub_master FROM PUBLIC;
```

- Strips the default `CONNECT`/`TEMP` grants every database gives to `PUBLIC`, so only explicitly granted roles can open the registry database.

```sql
GRANT CONNECT ON DATABASE cafehub_master TO cafehub_master_owner, cafehub_master_app;
```

- Re-grants connection exclusively to the two Master roles, making the allow-list explicit rather than the absence of a revoke.

```sql
REVOKE ALL ON SCHEMA public FROM PUBLIC;
```

- Removes any inherited `PUBLIC` access to the single schema every Master table lives in (the repository's one-`public`-schema convention); on PostgreSQL 15+ this is largely already true by default, but the explicit revoke makes the end state version-proof (section 9.1).

```sql
GRANT USAGE ON SCHEMA public TO cafehub_master_app;
```

- Lets the application role resolve objects in the schema without granting `CREATE` on the schema, which would let the application role create objects.

```sql
ALTER DEFAULT PRIVILEGES FOR ROLE cafehub_master_owner IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO cafehub_master_app;
```

- Ensures every table the owner role creates from now on (all migrations) is automatically readable and writable by the application role, without ever granting `CREATE`, `ALTER`, or `DROP`.

```sql
ALTER DEFAULT PRIVILEGES FOR ROLE cafehub_master_owner IN SCHEMA public
    GRANT USAGE ON SEQUENCES TO cafehub_master_app;
```

- Grants sequence usage so generated identity columns and `nextval` reads work for the application role while sequence definition changes stay owner-only.

**Order note.** The database `REVOKE ALL ... FROM PUBLIC` must precede the targeted `GRANT CONNECT`: while `PUBLIC` still holds `CONNECT`, every role in the cluster could open the registry, including future tenant application roles.

## 6. Tenant database creation (same least-privilege pattern)

Per tenant, in the provisioning role's session (one statement per `-c`):

```sql
CREATE DATABASE cafehub_tenant_<tenant-slug> OWNER cafehub_tenant_<tenant-slug>_owner;
```

- Creates one dedicated database per cafe (PRD section 9, rule 2 — one `Master/Registry` database and one `Tenant DB` per cafe), owned by that tenant's own owner role so no other tenant's owner role can touch it.

All remaining tenant commands run inside that tenant's database, in that tenant's owner role's session (`psql "host=<host> port=<port> dbname=cafehub_tenant_<tenant-slug> user=cafehub_tenant_<tenant-slug>_owner"`):

```sql
REVOKE ALL ON DATABASE cafehub_tenant_<tenant-slug> FROM PUBLIC;
```

- Strips `PUBLIC`'s default connect/usage access to this tenant's database, which is the database-level wall between tenants; without it the next tenant's application role would connect freely.

```sql
GRANT CONNECT ON DATABASE cafehub_tenant_<tenant-slug> TO cafehub_tenant_<tenant-slug>_owner, cafehub_tenant_<tenant-slug>_app;
```

- Limits connection to exactly this tenant's two roles.

```sql
REVOKE ALL ON SCHEMA public FROM PUBLIC;
```

- Keeps every role other than this tenant's own pair unable to enter this database's schema (and closes the pre-15 default, section 9.1).

```sql
GRANT USAGE ON SCHEMA public TO cafehub_tenant_<tenant-slug>_app;
```

- Lets this tenant's application role traverse the schema without holding schema `CREATE`.

```sql
ALTER DEFAULT PRIVILEGES FOR ROLE cafehub_tenant_<tenant-slug>_owner IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO cafehub_tenant_<tenant-slug>_app;
```

- Applies the same runtime surface as the Master: full data manipulation of this tenant's rows, zero DDL.

```sql
ALTER DEFAULT PRIVILEGES FOR ROLE cafehub_tenant_<tenant-slug>_owner IN SCHEMA public
    GRANT USAGE ON SEQUENCES TO cafehub_tenant_<tenant-slug>_app;
```

- Keeps generated-sequence reads working for this tenant's application role and nothing else.

The provisioning workflow that actually creates tenants is automated (FR-ONB-006); these commands are the reviewed statement of what it must execute, with `<tenant-slug>` replaced once per tenant when the statement set is generated. Retry safety belongs to the workflow itself (FR-ONB-007), not to these paragraphs.

**Rejected alternative.** Granting the application role `CREATE` on its schema "so app-side tooling works": the role would then be able to create objects (probe C3 below), breaking the migration-only DDL policy the repository's EF history depends on.

## 7. Required extensions

**No extension is required by these conventions.** Run no `CREATE EXTENSION` in the Master database, any tenant database, or the role-creation scripts.

- **Why.** Primary keys are application-generated UUIDv7 values (`Guid.CreateVersion7()` per `architecture/database-conventions.md`), so nothing like `pgcrypto`'s `gen_random_uuid()` or `uuid-ossp` is needed — and PostgreSQL ≥ 13 ships `gen_random_uuid()` in core anyway. Case-insensitive uniqueness is enforced with lowered storage plus `CHECK` constraints and plain unique indexes, or `lower()` expression indexes written by migrations — no `citext`. There is no other feature this document depends on.
- **Rejected alternative.** Installing an extension "for later use": every extension widens the server's executable surface for all tenants at once; the conventions' mechanism list needs none, so the least-privilege posture is to install none.

## 8. Local development versus production

**Local development.**

- Run the scripts against a disposable local cluster (for example one initialized with `initdb` into a temporary data directory and a non-default port), never against the machine's default service instance — this keeps local work immune to data you did not create and lets every experiment be destroyed cleanly (this document's verification record did exactly that).
- A local developer connects as the local administrator role to bootstrap roles, then supplies role passwords from User Secrets or prompt-on-session environment variables; the committed scripts in this document carry placeholders only.
- Tenant databases during development use throwaway slugs and are recreated freely; nothing outside your machine knows them.

**Production.**

- The scripts are replaced by the provisioning pipeline's programmatic execution of exactly these statements against the production cluster, using a credential from the production secrets store — manual interactive superuser sessions are not part of the designed workflow.
- Role passwords live only in the production secrets store; connection strings are resolved at runtime through `ConnectionStrings:Master` and the `TenantConnections:<SecretReference>` convention by the fail-closed resolver (`architecture/database-schema-decisions.md`). Raw connection strings are never stored as plain configuration values (PRD sections 10.2 and 11.1).
- Connectivity controls (host-based access rules, TLS, listen addresses) and backup/encryption specifics are deployment-level decisions outside this document — backup topics remain open under PRD TBD-19 — and are never replaced by these role grants.
- The manual isolation checks of section 9 run against production by operations before each release per the checklist in section 12, with per-role credentials from the secrets store: read-only probes plus deliberately denied connections that never touch data.

## 9. Manual isolation verification

This section is the proof that the layout above enforces database-level tenant isolation that the unit-test suite cannot observe (see the risk acceptance in `architecture/architecture-decisions.md` — database-level isolation is manually verified because the repository test policy limits Stage 1 testing to unit tests). Each check names the exact command, the expected result, and what a pass means. **An expected permission denial is a successful check only when the denial's exit status and message match the documented expectation; a successful command result is never silently treated as a pass.**

Checks are executed with each role's real password taken from the secret store (`PGPASSWORD` or the connection string), so a denial reflects the privilege policy rather than an authentication typo. What successful connections produce (`SELECT 1;`) is never used as evidence of anything except in the positive controls.

### Check C1 — Tenant A's application role cannot connect to Tenant B's database

```shell
psql "host=<host> port=<port> dbname=cafehub_tenant_<tenant-b-slug> user=cafehub_tenant_<tenant-a-slug>_app" -c "SELECT 1;"
```

- Needed because a leaked or mis-resolved credential is the realistic incident; this proves that even with Tenant A's application credential in hand, PostgreSQL refuses the other tenant's database.

Expected (with exit status `2`):

```text
psql: error: connection to server at "<host>", port <port> failed: FATAL:  permission denied for database "cafehub_tenant_<tenant-b-slug>"
DETAIL:  User does not have CONNECT privilege.
```

Pass condition: psql exits non-zero with exactly this FATAL and DETAIL pair. Fail condition: any successful `SELECT 1` output, or a different failure reason (for example a connection/authentication error, which proves nothing about isolation and means the check must be retried with the intended role password).

### Check C2 — Tenant A's application role cannot connect to the Master database

```shell
psql "host=<host> port=<port> dbname=cafehub_master user=cafehub_tenant_<tenant-a-slug>_app" -c "SELECT 1;"
```

- Needed because tenant application traffic must never reach the registry even under credential confusion; the Master database holds tenant identity and subscription state (FR-PLT-005) that tenant code must not read.

Expected (with exit status `2`): `FATAL: permission denied for database "cafehub_master"` with `DETAIL: User does not have CONNECT privilege.` Same pass/fail conditions as C1.

### Check C3 — Tenant A's application role cannot create objects

```shell
psql "host=<host> port=<port> dbname=cafehub_tenant_<tenant-a-slug> user=cafehub_tenant_<tenant-a-slug>_app" -c "CREATE TABLE must_not_exist ();"
```

- Needed because the application role holds data-manipulation rights only; proving the `CREATE` denial shows that the schema's DDL surface belongs to migrations alone and cannot be widened from application code.

Expected (with exit status `1`):

```text
ERROR:  permission denied for schema public
LINE 1: CREATE TABLE must_not_exist ();
```

Pass condition: the command fails with this exact error, and no table named `must_not_exist` exists afterwards. Fail condition: the table is created, meaning the app role holds schema `CREATE` (the section 6 `REVOKE`/`GRANT` division did not apply).

### Check C4 — `has_database_privilege` reports `false` for databases Tenant A must not access

```shell
psql "host=<host> port=<port> dbname=cafehub_tenant_<tenant-a-slug> user=cafehub_tenant_<tenant-a-slug>_app" -v ON_ERROR_STOP=1 -c "SELECT has_database_privilege('cafehub_tenant_<tenant-a-slug>_app', 'cafehub_tenant_<tenant-b-slug>', 'CONNECT') AS tenant_b_connect, has_database_privilege('cafehub_tenant_<tenant-a-slug>_app', 'cafehub_master', 'CONNECT') AS master_connect, has_database_privilege('cafehub_tenant_<tenant-a-slug>_app', 'cafehub_tenant_<tenant-a-slug>', 'CONNECT') AS own_connect;"
```

- Needed because this query inspects the privilege ACLs directly instead of attempting a connection, so it detects a wrongly-open database without needing to guess at psql's exit codes; the own-database column is a control that proves the function itself is being read truthfully.

Expected:

```text
 tenant_b_connect | master_connect | own_connect
-----------------+----------------+-------------
 f               | f              | t
(1 row)
```

Pass condition: exactly `f`, `f`, `t`. Fail condition: any other value pattern, or an error — an error means the command was run against the wrong database or role, not that isolation failed.

### Check C5 — The Master application role cannot connect to tenant databases

```shell
psql "host=<host> port=<port> dbname=cafehub_tenant_<tenant-a-slug> user=cafehub_master_app" -c "SELECT 1;"
```

- Needed because tenant databases must be reachable only by their own pair of roles; a Master-side credential with tenant reach could read every cafe's operational data in one place — the exact opposite of the database boundary.

Expected (with exit status `2`): `FATAL: permission denied for database "cafehub_tenant_<tenant-a-slug>"` with `DETAIL: User does not have CONNECT privilege.` Repeat the same command with `dbname=cafehub_tenant_<tenant-b-slug>` for the second tenant; the identical denial is expected for each tenant database present.

### Positive controls (prove the denials are policy, not breakage)

```shell
psql "host=<host> port=<port> dbname=cafehub_tenant_<tenant-a-slug> user=cafehub_tenant_<tenant-a-slug>_app" -v ON_ERROR_STOP=1 -c "SELECT count(*) FROM permission_probe;"
```

- Needed because an environment where everything is denied looks identical in exit codes to a broken one; this control proves Tenant A's application role can reach and read its own data through the default-privilege grants.

Expected (exit status `0`): a count value for the probe table. (At first provisioning this table may not exist yet — the migration history creates the real tables; for the probe pattern any one existing table of the tenant suffices.)

```shell
psql "host=<host> port=<port> dbname=cafehub_master user=cafehub_master_app" -v ON_ERROR_STOP=1 -c "SELECT current_user;"
```

- Needed to prove the Master application role's own connection path is healthy, so a denial in C5 reads as a per-database policy result and not as a broken Master login.

Expected (exit status `0`): `cafehub_master_app`.

### 9.1 PostgreSQL 15+ public-schema privilege differences

The commands above are correct on both sides of the PostgreSQL 15 boundary, but the reason differs:

- **PostgreSQL 14 and earlier:** new databases grant `CONNECT` (and `TEMP`) on the database to `PUBLIC` by default, and the `public` schema grants both `USAGE` and `CREATE` to `PUBLIC`. The explicit revokes in sections 5 and 6 are therefore load-bearing: without them every cluster role could connect to every database and create objects inside every schema.
- **PostgreSQL 15 and later:** fresh clusters (initdb-created databases inherit from the modified templates) still grant `CONNECT`/`TEMP` on the database to `PUBLIC`, but `public`'s ownership moves to `pg_database_owner` (so the database's owner role owns the schema) and `PUBLIC` keeps only `USAGE` on the schema — its `CREATE` is stripped by default. The schema-usage grant to the application role (and the explicit `REVOKE ALL ... FROM PUBLIC` backstop) remains necessary because `USAGE` legitimately stays with `PUBLIC` by default. Databases upgraded in place from 14 (pg_upgrade) keep the pre-15 ACLs; re-run the revokes there too.
- **Practical consequence:** restoring a pre-15 dump into a 15+ cluster (or a `pg_upgrade` from an older cluster) does not restore the documented end state automatically — the revokes and regrants in sections 5 and 6 must be re-executed after any restore, and the source cluster's version must be recorded with the backup. Database `CONNECT` revocation is always the active per-database wall on every supported version — the schema-level difference alone is not an isolation control.

## 10. Cleanup commands for the disposable test environment

Order matters: drop the databases first (the owner roles cannot be dropped while they own live databases), then drop every role. Run the cleanup as an administrator against the cluster maintenance database. Each statement gets its own psql `-c` because `DROP DATABASE` cannot run inside the implicit transaction block a combined `-c "A; B;"` statements creates:

```shell
psql "host=<host> port=<port> dbname=postgres user=<admin-role-name>" -v ON_ERROR_STOP=1 \
  -c "DROP DATABASE IF EXISTS cafehub_master;" \
  -c "DROP DATABASE IF EXISTS cafehub_tenant_<tenant-a-slug>;" \
  -c "DROP DATABASE IF EXISTS cafehub_tenant_<tenant-b-slug>;"
```

- Drops the databases so their owner roles become droppable; `IF EXISTS` makes the cleanup re-runnable after a partial attempt.

```shell
psql "host=<host> port=<port> dbname=postgres user=<admin-role-name>" -v ON_ERROR_STOP=1 \
  -c "DROP ROLE cafehub_master_app;" \
  -c "DROP ROLE cafehub_master_owner;" \
  -c "DROP ROLE cafehub_tenant_<tenant-a-slug>_app;" \
  -c "DROP ROLE cafehub_tenant_<tenant-a-slug>_owner;" \
  -c "DROP ROLE cafehub_tenant_<tenant-b-slug>_app;" \
  -c "DROP ROLE cafehub_tenant_<tenant-b-slug>_owner;" \
  -c "DROP ROLE cafehub_provisioning;"
```

- Removes every role minted for the experiment so no leftover credential survives the environment (dropping the provisioning role also removes its membership grants in the owner roles automatically).

**Disposable local cluster teardown** (when the whole cluster was created for the check): stop the server, then delete the data directory:

```shell
pg_ctl -D <disposable-datadir> stop -m fast
Remove-Item (or equivalent) -Recurse -Force <disposable-datadir>
```

- Deletes the cluster's data beyond recovery so no role password or test row outlives the environment.

Expected end state: all `DROP` statements succeed, the server stops, the data directory is gone, and a connection attempt to the local port now fails ("connection refused"/"no response"). This document's verification record followed exactly this sequence and confirmed it.

## 11. Known limitations

- **Database-level isolation is verified manually, not by automated tests.** The repository's accepted-risk statement on this gap is recorded in `architecture/architecture-decisions.md`; the checks here must run before each release and do not create a regression signal. Automated cross-tenant isolation tests with at least two tenants remain the PRD's requirement (FR-PLT-002); the future dedicated isolation test work is the planned closure of this gap, and the risk acceptance entries in `architecture/architecture-decisions.md` record the decision and the compensating controls in the meantime.
- **The checks prove per-database CONNECT and schema-CREATE denials only.** Row-level and column-level behaviors of application-level queries, future views, or anything the later stories introduce is outside this manual script's scope.
- **Network- and host-level controls are not covered here.** Host-based access rules, TLS configuration, listen addresses, and firewall segmentation are deployment decisions; the role grants in this document are the database-level layer of the isolation design, not a substitute for network hardening.
- **Tenant database naming from slugs is not injective** (`a-b` and `a_b` map to the same key; section 1). Provisioning resolves collisions and records the final database name; do not reconstruct data-location logic from the name.
- **A restore from an older major version can carry the source server's default privileges into the new cluster** (section 9.1), including re-granting `PUBLIC` on the `public` schema if the source dump predates 15; restoring the documented end state requires re-running sections 5 and 6 and then the section 9 checks.
- **Migrations must actually run as the owner roles.** A migration accidentally run as a superuser or an application-role session would bypass these default-privilege constructions and leave future tables un-granted; the runtime fail-closed design (`architecture/database-schema-decisions.md`) builds on the split (design-time tooling uses its own connection strings, run-time roles-only grants as here).
- Credential rotation and backup/encryption specifics are deployment policy — secret storage follows the PRD section 11.1 (Security) guardrails, and backup topics remain open under TBD-19.

## 12. Manual verification checklist (run before each release)

Every row states the command to run (sections have the full syntax with placeholders) and the exact expected result. Record date, executor, and PostgreSQL major version with each run.

| # | Check | Where to run | Command (abbreviated — full form in section) | Expected result | Overall result |
|---|---|---|---|---|---|
| C1 | Tenant A app blocked from Tenant B DB | ops workstation or local cluster | section 9/C1 | `FATAL: permission denied for database "cafehub_tenant_<tenant-b-slug>"` + `User does not have CONNECT privilege.`, exit 2 | ☐ |
| C2 | Tenant A app blocked from Master DB | ops workstation or local cluster | section 9/C2 | `FATAL: permission denied for database "cafehub_master"` + same DETAIL, exit 2 | ☐ |
| C3 | Tenant A app blocked from DDL in own DB | tenant A DB | section 9/C3 | `ERROR: permission denied for schema public`, exit 1; no table created | ☐ |
| C4 | `has_database_privilege` ACL summary | tenant A DB | section 9/C4 | `f`, `f`, `t` exactly | ☐ |
| C5 | Master app blocked from tenant DBs | each tenant DB | section 9/C5 | per-tenant `FATAL: permission denied for database ...` + DETAIL, exit 2 | ☐ |
| P1 | Tenant A app reads own data | tenant A DB | section 9/positive control | exit 0, count query succeeds | ☐ |
| P2 | Master app connects to Master DB | Master DB | section 9/positive control | exit 0, `cafehub_master_app` | ☐ |
| X1 | Disposable environment destroyed after checks | local teardown | section 10 | `DROP` messages + server stop; data directory gone; port refuses connections | ☐ |

Pass rule: a checklist row marks pass only when the observed output matches the documented expected result for that row; any deviation stops release sign-off until it is explained and remedied — under PRD section 14 (MVP Acceptance and Release Gate) an exception requires a documented owner and explicit risk acceptance.
