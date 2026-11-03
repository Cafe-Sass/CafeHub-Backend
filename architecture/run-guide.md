# Run guide

This document explains how to run the CafeHub backend locally, from prerequisites through the journey from admin login to a working cafe. All steps use only configuration names that exist in `src/CafeHub.WebApi/appsettings.template.json` and `CafeHub.Infrastructure.ConfigurationKeys`. Secret names are shown; secret values are placeholders only.

## Prerequisites

- .NET 10 SDK, pinned by `global.json` (currently 10.0.401, `rollForward: latestFeature`): check with `dotnet --list-sdks`
- Git
- The global `dotnet-ef` tool at the same version as, or newer than, the EF Core packages (currently 10.0.13): needed only for migrations
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

## Local PostgreSQL setup

PostgreSQL setup commands are described in `database/postgresql-commands.md` (database conventions reference). The `TenantDesignTime` connection string is required only for EF Core design-time tooling (migrations) and is never used at runtime.

> The repository does not contain a `database/postgresql-commands.md` file at the time of this writing; follow the conventions in `architecture/database-conventions.md` and `architecture/database-schema-decisions.md` instead.

## Migrations

Migration conventions, contexts, commands, and the design-time connection contract are in `architecture/database-schema-decisions.md`. No migration exists yet by design; the first approved entity starts the first migration. Migrations for already-provisioned tenant databases have no mechanism yet (see open items in `architecture/open-items-register.md`).

## Run the API

```bash
dotnet run --project src/CafeHub.WebApi
```

It starts and listens (launch profile `http` binds `http://localhost:5088`). Local secrets are not required for start-up; connection strings are resolved lazily when database work is actually exercised, and a missing setting then fails closed with a clear exception naming the missing key.

## Provisioning worker

The provisioning worker creates tenant databases and provisions cafes from approved onboarding requests. Run it from the host (it is a hosted service registered in `CafeHub.Infrastructure`). Run it twice with no duplicates to verify idempotency (see the final verification report for the observed result).

## Journey: admin login to a working cafe

The real order of operations, from platform setup to a working tenant cafe:

1. Create the databases (Master and the first tenant database).
2. Apply Master migrations.
3. Set secrets (user secrets or environment variables as listed above).
4. Start the host (`dotnet run --project src/CafeHub.WebApi`); the first Super Admin is seeded.
5. Admin login: `POST api/v1/admin/auth/login`.
6. Admin refresh, logout, and `me`: `POST api/v1/admin/auth/refresh`, `POST api/v1/admin/auth/logout`, `GET api/v1/admin/me`.
7. Create a plan: `POST api/v1/admin/plans`.
8. Register an owner: `POST api/v1/onboarding/requests`.
9. Pay: `POST api/v1/onboarding/requests/{id}/payments` with an `Idempotency-Key` header.
10. Approve: `POST api/v1/admin/onboarding-requests/{id}/approve`.
11. Run the provisioning worker.
12. Check status: `GET api/v1/onboarding/requests/{id}/status`.
13. Owner login on `/t/{slug}`: `POST t/{slug}/api/v1/auth/login`.
14. Tenant refresh, logout, and `me`: `POST t/{slug}/api/v1/auth/refresh`, `POST t/{slug}/api/v1/auth/logout`, `GET t/{slug}/api/v1/me`.
15. Cafe profile: `GET t/{slug}/api/v1/cafe-profile` and `PUT t/{slug}/api/v1/cafe-profile`.
16. Branches: `POST`, `GET`, `GET {id}`, `PUT`, `POST /suspend`, `POST /reactivate`, `POST /retire` on `t/{slug}/api/v1/branches`.

Use placeholder values only; no real emails, passwords, or keys. Cross-tenant attempts (token of cafe A on cafe B) return NotFound by database isolation.

## What is not covered

- The real data-access isolation boundary is verified by hand; no integration or database-backed tests exist in the repository.
- No payment provider is chosen; the mock provider is used locally (`Payments.Provider: "Mock"`).
- No notification provider is chosen.
- Migration mechanism for already-provisioned tenant databases does not exist yet.
- CORS origins and provisioning secrets that each environment must set are open configuration decisions.
