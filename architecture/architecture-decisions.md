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

- The backend uses CQRS with MediatR pinned to exactly 12.5.0 (last Apache-2.0 release; 13 or later is forbidden) plus FluentValidation, both installed through `Directory.Packages.props` (versions and approval rules live in the dependency policy). Command and query contracts with their handler contracts live in `CafeHub.Application.Abstractions.Messaging`, and every request returns `Result` or `Result<T>`.
- Maps (AutoMapper/Mapster) and generic repository wrappers stay out of scope here per Stage 1 constraints.

## Error codes

Every use case returns a typed `Result` from the namespace `CafeHub.Application.Abstractions.Results` instead of using exceptions for expected failures. A success carries no error; a failure carries exactly one `Error` with a stable machine-readable `Code`, a human-readable `Message`, and an `ErrorType` (`Failure`, `Validation`, `NotFound`, `Conflict`, `Unauthorized`, `Forbidden`).

- Error codes follow the convention `<Module>.<Reason>`, for example `Tenant.NotFound`.
- A module declares its errors in exactly one static class named `<Module>Errors` inside the module folder. That class exposes ready-made `Error` factories or values so every call site reuses the same codes.
- Error messages are safe for clients: they never contain secrets, connection strings, stack traces, or raw exception text.
- Validation failures use `ValidationError` with the code `Validation.Failed`; it carries one `FieldError` (field name plus message) per invalid field.

## Error responses to clients

Every failed `Result` reaches clients in one RFC 9457 ProblemDetails shape. The mapper `ErrorProblemDetailsMapper` in `src/CafeHub.WebApi/Errors` is the single mapping place (CFMD-246); controllers use the helper `Result.ToProblemResult(HttpContext)` and never build error JSON by hand.

| ErrorType | Status | Title | `type` link |
|---|---|---|---|
| Validation | 400 | Bad Request | `https://tools.ietf.org/html/rfc9110#section-15.5.1` |
| Unauthorized | 401 | Unauthorized | `https://tools.ietf.org/html/rfc9110#section-15.5.2` |
| Forbidden | 403 | Forbidden | `https://tools.ietf.org/html/rfc9110#section-15.5.4` |
| NotFound | 404 | Not Found | `https://tools.ietf.org/html/rfc9110#section-15.5.5` |
| Conflict | 409 | Conflict | `https://tools.ietf.org/html/rfc9110#section-15.5.10` |
| Failure | 500 | Internal Server Error | `https://tools.ietf.org/html/rfc9110#section-15.6.1` |

- The table is mirrored in the mapper's XML summary; the two must stay identical.
- Every ProblemDetails carries the machine-readable `code` extension (the `Error.Code`) and a `traceId` extension (`Activity.Current?.Id ?? HttpContext.TraceIdentifier`); a correlation identifier is a later story.
- Validation responses use `ValidationProblemDetails`: the `Errors` dictionary lists every field-level message in validator order.
- Titles and `type` links mirror the RFC 9110 defaults of ASP.NET Core (`ProblemDetailsDefaults`), so standard ASP.NET Core client-error links stay consistent.

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

- Result/Error model placement — the model lives in Application (CFMD-241); Domain cannot reference it until a later decision.
- Tenant identifier source (host, header, or path) — required before tenant resolution middleware; PRD has no TBD number for it.
- API versioning style — required before first business endpoint (PRD section 10.4).
- Notification provider/channels — PRD TBD-09.
- Payment provider(s) — PRD TBD-02.

None of these may be invented during scaffolding; each is decided by its PRD owner with a recorded decision.
