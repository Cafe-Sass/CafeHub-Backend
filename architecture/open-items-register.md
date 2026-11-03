# Open items register

Story CFMD-353 (BE - Delivery and Handover), Epic CFMD-318.

Collected from the `Assumptions and open items` sections of every merged PR in this epic (verified via `gh pr list --state merged` and `gh pr view <number>`), plus items carried over from the story prompt and the architecture/ERD documents. Each item has an id, the PR that raised it, and a proposed next step labelled "Proposed, pending owner confirmation".

Items are grouped by area: decisions pending, TBDs from the PRD, risks / accepted risks, and follow-ups. Manual-verification refactor PRs #83 and #84 are called out explicitly.

## Decisions pending (owner confirmation)

| Id | Item | Raised by | Proposed next step |
|---|---|---|---|
| OI-001 | Branch name uniqueness: the Tenant ERD defines no unique constraint on `branches.name` and the PRD does not require one, so no unique index or migration was added; duplicate branch names are allowed. | PR #96 (CFMD-349), also PR #99 and PR #102 | Proposed, pending owner confirmation: owner decides whether to add a case-insensitive unique index on `branches.name`; doing so requires an ERD amendment and a Tenant migration. Rejected alternative: silently add a unique index now. |
| OI-002 | `Swashbuckle.AspNetCore.SwaggerUI` 10.3.1 is installed in `Directory.Packages.props` but was missing from `architecture/dependency-policy.md`; added as Proposed because the OpenAPI prompt did not approve a viewer. The Agent never approves a package. | PR #101 (CFMD-357) | Proposed, pending owner confirmation: owner approves or removes the package and updates `Directory.Packages.props` accordingly. |
| OI-003 | `Microsoft.AspNetCore.Authentication.JwtBearer` is marked "(proposed, pending owner confirmation...)" in the existing dependency-policy table. | PR #101 (CFMD-357) / existing policy | Proposed, pending owner confirmation: owner approves the package. |
| OI-004 | Warnings-as-errors not enabled in this story. | PR #100 (CFMD-356) | Proposed, pending owner confirmation: enable `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` as a follow-up once all warnings are cleaned. |
| OI-005 | Production-code warning `OwnerPasswordHasher.cs(12,40)` CS8625 (cannot convert null literal to non-nullable reference type) is out of scope for test-project warning cleanup. | PR #100 (CFMD-356) | Proposed, pending owner confirmation: fix in a follow-up story. |
| OI-006 | `BranchLifecycleStatus` modeled as `BranchLifecycleStatus?` on `ListBranchesQuery.Status`; unknown enum values map to a `status` field error that never echoes the value. If the owner prefers `status` as a raw string parsed in the handler, that is a separate decision. | PR #97 (CFMD-350) | Proposed, pending owner confirmation: owner confirms the `Status` model choice. |
| OI-007 | Lifecycle action route names `POST /branches/{id}/suspend`, `/reactivate`, `/retire` are Proposed; rejected alternative: a `PATCH` with a status body. | PR #99 (CFMD-352) | Proposed, pending owner confirmation: owner confirms route names. |
| OI-008 | The repository does not contain a `database/postgresql-commands.md` file; the run-guide directs the reader to `architecture/database-conventions.md` and `architecture/database-schema-decisions.md` instead. | PR #102 (CFMD-354) | Proposed, pending owner confirmation: owner decides whether to add `database/postgresql-commands.md`. |
| OI-009 | CORS origins (`Cors:AllowedOrigins`) and provisioning secrets that each environment must set are open configuration decisions. | PR #102 (CFMD-354) | Proposed, pending owner confirmation: owner sets CORS origins and provisioning secrets per environment. |
| OI-010 | Migrations for already-provisioned tenant databases have no mechanism yet. | PR #102 (CFMD-354) / story carry-over | Proposed, pending owner confirmation: owner decides on a migration mechanism for provisioned tenant databases. |

## TBDs from the PRD

| Id | Item | Raised by | Proposed next step |
|---|---|---|---|
| OI-012 | Branch name uniqueness is listed as "Proposed decision for the owner" in the story (the ERD and PRD leave it unresolved). | Story CFMD-348 / PR #96 | Same as OI-001; owner decides. No separate TBD number exists in the PRD for this item. |

## Risks / accepted risks

| Id | Item | Raised by | Proposed next step |
|---|---|---|---|
| OI-011 | Isolation Risk Acceptance: unit tests cover the request and authorization boundaries; the data-access boundary is verified by manual check only (no integration/database-backed tests exist). | Story CFMD-348 carry-over / PR #99 | Proposed, pending owner confirmation: owner confirms acceptance of manual-check coverage for the data-access boundary. |

## Manual-verification refactor pull requests (called out explicitly)

The two manual-verification refactor pull requests fixed defects found by hand:

| PR | Title | Defects fixed |
|---|---|---|
| #83 `Rafactor` | fix(onboarding): persist provisioning and payment state | Provisioning and payment state persistence defects found by hand |
| #84 `Refactor` | fix: resolve tenant login permissions and provisioning issues | Tenant credential scoping, tenant role schema access, plan version ids in responses defects found by hand |

## PRD requirement ID mapping

Requirement IDs verified by grep in `Docs/Prd.md` (referenced by ID only; PRD text not quoted):

| Requirement ID | Story / sub-task that delivered it | Status | Reason |
|---|---|---|---|
| FR-CAF-002 | CFMD-348 (sub-tasks CFMD-349, CFMD-350, CFMD-351, CFMD-352) | Delivered | Branch create, list, get, update, lifecycle (suspend/reactivate/retire), concurrency, audit, no physical delete, no hard-coded quota |
| FR-CAF-001 | CFMD-345 / CFMD-346 / CFMD-347 (Prompt 35, cafe profile) | Delivered | Cafe profile create/update/query — prior epic story, not part of CFMD-348 |
| FR-IAM-005 | CFMD-331 (tenant audit writer) | Delivered | Tenant audit actor identity; branch audit entries (`branches.created.succeeded`, `branches.updated.succeeded`, `branches.suspended.succeeded`, `branches.reactivated.succeeded`, `branches.retired.succeeded`) added in CFMD-348 |
| FR-AUD-001 | CFMD-348 (branch lifecycle + create/update audit) | Partially delivered | Sensitive action audit implemented for branches only; other modules deferred to later stories per the ERD |
| FR-CAF-003 | Not addressed in CFMD-348 | Not delivered | Table management belongs to a later story |
