# CafeHub-Backend

The CafeHub backend is a multi-tenant SaaS API for cafe management, structured as a Clean Architecture Application-layer modular monolith in one deployable ASP.NET Core Web API. Tenant isolation is fail-closed: every tenant connection is resolved from a validated tenant context, never from a fallback default. Tenant identification mechanism, payment provider, notification provider, and subscription pricing are still open decisions and are not implemented.

## Technology stack

- .NET 10 (`net10.0`, ASP.NET Core Web API with Controllers only; block-scoped namespaces)
- PostgreSQL through EF Core + Npgsql (SQL Server is forbidden)
- CQRS planned with MediatR pinned to exactly 12.5.0 plus FluentValidation (approved, installed in Sprint 2)
- xUnit unit tests (hand-written fakes; no Integration, Architecture, or database-backed tests)
- Solution layout: `src/CafeHub.Domain`, `src/CafeHub.Application`, `src/CafeHub.Infrastructure`, `src/CafeHub.WebApi`, `tests/CafeHub.UnitTests`; decision records live in `architecture/`

## Prerequisites

- .NET 10 SDK, pinned by `global.json` (currently 10.0.401, `rollForward: latestFeature`): check with `dotnet --list-sdks`
- Git
- The global `dotnet-ef` tool at the same version as, or newer than, the EF Core packages (currently 10.0.13) — needed only for migrations:
  ```bash
  dotnet tool install --global dotnet-ef
  # or update an existing installation
  dotnet tool update --global dotnet-ef
  ```
- PostgreSQL — needed only later for database work; not needed to build or test

## Clone and open

```bash
git clone https://github.com/Cafe-Sass/CafeHub-Backend.git
cd CafeHub-Backend
```

Open the repository with an editor; the SDK is pinned by `global.json` and the classic `CafeHub.Backend.sln` sits at the root.

## Local configuration

No credential is ever committed. Copy the placeholder values from `src/CafeHub.WebApi/appsettings.template.json` and set the real values locally. Setting names equal the constants in `CafeHub.Infrastructure.ConfigurationKeys`. Secrets are needed only for database work (running the API or migrations), not for building or testing.

User secrets (run from the repository root; `<...>` are placeholders):

```bash
dotnet user-secrets set "ConnectionStrings:Master" "<master-connection-string>" --project src/CafeHub.WebApi
dotnet user-secrets set "ConnectionStrings:TenantDesignTime" "<tenant-design-time-connection-string>" --project src/CafeHub.WebApi
dotnet user-secrets set "TenantDatabases:Host" "<postgres-host>" --project src/CafeHub.WebApi
dotnet user-secrets set "TenantDatabases:Port" "5432" --project src/CafeHub.WebApi
dotnet user-secrets set "TenantDatabases:SslMode" "Prefer" --project src/CafeHub.WebApi
dotnet user-secrets set "TenantDatabases:MaxPoolSize" "10" --project src/CafeHub.WebApi
dotnet user-secrets set "Jwt:Issuer" "<jwt-issuer>" --project src/CafeHub.WebApi
dotnet user-secrets set "Jwt:Audience" "<jwt-audience>" --project src/CafeHub.WebApi
dotnet user-secrets set "Jwt:SigningKey" "<jwt-signing-key>" --project src/CafeHub.WebApi
```

Equivalent environment variables (`__` is the level separator):

```text
ConnectionStrings__Master
ConnectionStrings__TenantDesignTime
TenantDatabases__Host
TenantDatabases__Port
TenantDatabases__SslMode
TenantDatabases__MaxPoolSize
Jwt__Issuer
Jwt__Audience
Jwt__SigningKey
```

Tenant connection strings are assembled at runtime from the validated tenant registry record and the shared provisioning credential derivation. The `TenantDatabases` values come from User Secrets locally and environment variables in deployed environments. Configure `TenantDatabases:SslMode` as `Require` or stronger in deployed environments; `Prefer` is the local default for development PostgreSQL instances without TLS.

The `Jwt:SigningKey` must be at least 32 bytes when UTF-8 encoded. It comes from User Secrets locally and from an environment variable in deployed environments; it is never written to a tracked file, logged, or returned. Issuer, audience, and lifetime are validated at startup and the host refuses to start when they are missing or weak.

## Verify (identical to CI)

Run from the repository root, in this order — the same commands as `.github/workflows/ci.yml`:

```bash
dotnet restore CafeHub.Backend.sln
dotnet format CafeHub.Backend.sln --verify-no-changes --no-restore
dotnet build CafeHub.Backend.sln --no-restore --configuration Release
dotnet test tests/CafeHub.UnitTests/CafeHub.UnitTests.csproj --no-build --configuration Release --verbosity normal --logger "trx;LogFileName=unit-tests.trx" --results-directory TestResults
```

The `dotnet test` flags after `--no-build` produce a TRX report exactly as CI does; the same test command without those flags also works locally. `TestResults/` is gitignored.

## Run the API

```bash
dotnet run --project src/CafeHub.WebApi
```

It currently starts and listens (launch profile `http` binds `http://localhost:5088`) with no endpoints yet; real endpoints arrive with business stories. Local secrets are not required for start-up; connection strings are resolved lazily when database work is actually exercised, and a missing setting then fails closed with a clear exception naming the missing key.

## Migrations

Migration conventions, contexts, commands, and the design-time connection contract are in `architecture/database-schema-decisions.md`. No migration exists yet by design; the first approved entity starts the first migration.

## Team conventions

- Branch names: `Feature/<Number>_SA_<PascalName>`, where `<Number>` is the Jira number without the `CFMD-` prefix (example: `Feature/208_SA_AlignDbSchemaDecisions`).
- Pull-request title is identical to the branch name.
- Commits use Conventional Commits `<type>(<scope>): <description>` with the footer `Refs: CFMD-<number>`.
- One pull request per sub-task into `development`; no direct pushes to `development`.
- Pull-request descriptions use the agreed standard sections (Task, What changed, Why, API & Contracts, Database / Tenant impact, Tests, Verification results, Assumptions and open items, Risks & Impact, Review focus, Checklist).
- C# code uses block-scoped namespaces (`namespace CafeHub.Application { ... }`).
- Every commit must build and keep all unit tests passing.

## CI

The `CI` workflow (`.github/workflows/ci.yml`) runs on pull requests to `development` and on pushes to `development`. It checks out the code, sets up the .NET SDK from `global.json`, restores, verifies formatting, builds Release, runs `CafeHub.UnitTests` with a TRX report, appends Total/Passed/Failed into the run summary, and uploads `TestResults/` as an artifact even when tests fail. It requires no secrets and runs with read-only repository permissions. The unit-test step runs in seconds (no database services are involved), and the job carries a 15-minute timeout.

## Private documentation

`Docs/` is local-only, gitignored, and never committed. Its contents (the PRD and related material) are not reproduced in the repository; tracked files cite PRD requirement identifiers instead.
