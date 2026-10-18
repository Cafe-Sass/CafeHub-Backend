# Architecture Decisions

Organizational baseline for the CafeHub backend, derived from the CafeHub PRD (CH-PRD-001 v1.0) and the Stage-1 backend story. Companion document: `architecture/database-schema-decisions.md`.

## Structure

- The backend follows Clean Architecture with the solution `CafeHub.Backend.sln` containing `CafeHub.Domain`, `CafeHub.Application`, `CafeHub.Infrastructure`, `CafeHub.WebApi`, and the single test project `CafeHub.UnitTests` (all `net10.0`).
- Backend WebApi is the only deployable for the MVP. Modules are not microservices (PRD section 10.1).

## Layers and reference direction

| Layer | Depends on | Role |
|---|---|---|
| Domain | nothing | Business rules only |
| Application | Domain | Use cases, orchestration, module boundaries |
| Infrastructure | Application, Domain | EF Core, external services, configuration |
| WebApi | Application, Infrastructure | Composition root and HTTP surface only |

- Allowed references are exactly those rows; anything else (for example WebApi referencing Domain, or tests referencing WebApi) is rejected in review.
- CafeHub.WebApi is created in sub-task BE_Scaffold_WebApi_Project (CFMD-206); only the class libraries and the unit test project exist at this stage so Application and Infrastructure can be authored in parallel without depending on a host.
- Shared files `Directory.Build.props`, `Directory.Packages.props`, and `.editorconfig` arrive in a dedicated conventions task (CFMD-212); package versions are inline in project files until then.

## Application-layer Modular Monolith

- Business code lives in Application organized as modules with explicit boundaries. The planned module folders of the one deployable backend are:

  - `Tenancy` (tenant context contracts; Database & Tenant domain is the infrastructural side)
  - `Onboarding` (registration, plan selection, approval flow, provisioning requests)
  - `Subscriptions` (plan catalog, subscription lifecycle, initial payment states)
  - `Identity` (owner and staff authentication and permission boundaries)
  - `CafeConfiguration` (profile/branding, branches, tables, QR generation)
  - `Menu` (catalog, branches availability, OCR draft import review)
  - `Ordering` (guest and Captain-assisted orders, acceptance, Order More)
  - `Billing` (bills, payment records, Hybrid settlement, receipts)
  - `WiFi` (packages, token issuance)
  - `Inventory` (operational stock only; no Sprint 1 tables)
  - `Attendance` (QR check-in/out)
  - `Reporting` (dashboard metrics, insight cards)
  - `Notifications` (provider-agnostic boundary contract only)
  - `Printing` (local print job dispatch boundaries)

- The list is organisational only. No module folder, entity, DTO, or use case is created in this sub-task; folders appear when real work for a module begins.

## CQRS and validation

- The backend plans CQRS with MediatR pinned to exactly 12.5.0 (last Apache-2.0 release; 13 or later is forbidden) plus FluentValidation. Both are approved; neither is installed yet. They arrive with the first real use cases in Sprint 2, not in foundation scaffolding.
- Maps (AutoMapper/Mapster) and generic repository wrappers stay out of scope here per Stage 1 constraints.

## Testing

- The repository has exactly one test project, `tests/CafeHub.UnitTests`, using xUnit v2 with hand-written fakes. No Integration tests, no Architecture tests, no Testcontainers, no in-memory/SQLite database providers, no mocking library, and nothing that requires a running database or network at Stage 1.
- Accepted risk: real database-level isolation cannot be proven by unit tests. The PRD requires automated cross-tenant isolation tests with at least two isolated tenants covering request, authorization, and data-access boundaries (FR-PLT-002). At foundation stage those boundaries are covered only at logic level: fail-closed connection resolution, missing/invalid/inactive context handling, and no cross-tenant fallback are unit-tested with fakes, while true database-level proof waits for the dedicated isolation test work. This gap is deliberate and recorded, not silently dropped.

## Ownership

| Item | Owner role | Named assignee |
|---|---|---|
| `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, build workflows | Build & DevEx owner | TBD |
| `architecture/*.md` decision records | Architecture owner | TBD |

## Local configuration

The WebApi reads real credentials locally through User Secrets or environment variables; no credential is committed to Git.

- Required setting keys (names equal the constants in `CafeHub.Infrastructure.ConfigurationKeys`):
  - `ConnectionStrings:Master`
  - `ConnectionStrings:TenantDesignTime`
  - `TenantConnections:<SecretReference>` — one connection string per tenant secret reference
- Set locally with User Secrets (placeholders in `<>`; replace with real local values, never committing them):

  ```text
  dotnet user-secrets set "ConnectionStrings:Master" "<master-connection-string>" --project src/CafeHub.WebApi
  dotnet user-secrets set "ConnectionStrings:TenantDesignTime" "<tenant-design-time-connection-string>" --project src/CafeHub.WebApi
  dotnet user-secrets set "TenantConnections:<SecretReference>" "<tenant-connection-string>" --project src/CafeHub.WebApi
  ```

- Equivalent environment variable names (`__` is the multi-level separator):
  - `ConnectionStrings__Master`
  - `ConnectionStrings__TenantDesignTime`
  - `TenantConnections__<SecretReference>`
- `src/CafeHub.WebApi/appsettings.template.json` lists every key the implementation expects with placeholders; the file name is not loaded by ASP.NET Core automatically. Design-time migrations additionally require the `ConnectionStrings__TenantDesignTime` environment variable (see TenantDbContextFactory). The full onboarding README arrives with CFMD-218.

## Open decisions

- Tenant identifier source (host, header, or path) — required before tenant resolution middleware; PRD has no TBD number for it.
- API versioning style — required before first business endpoint (PRD section 10.4).
- Notification provider/channels — PRD TBD-09.
- Payment provider(s) — PRD TBD-02.

None of these may be invented during scaffolding; each is decided by its PRD owner with a recorded decision.
