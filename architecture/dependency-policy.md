# Dependency Policy

How packages are versioned, approved, and audited for the CafeHub backend. Companion document: `architecture/architecture-decisions.md`.

## Purpose and mechanism

- Package versions are managed centrally with `Directory.Packages.props` (`ManagePackageVersionsCentrally=true`); project files contain `PackageReference` items without inline `Version` attributes.
- Shared solution files — `Directory.Build.props`, `Directory.Packages.props`, and `.editorconfig` — are owned by the **Build & DevEx owner** role (assigned owner: TBD).
- Every package, including test-only packages, is added or bumped through the approval process below; direct project-file versioning is rejected in review.

## Approval process

1. State the purpose and need: which story requires the package and why an existing package or framework capability does not cover it.
2. Confirm compatibility with `net10.0` and the current SDK (`global.json` pins the supported SDK; `Directory.Build.props` sets the target framework).
3. License check: permissive open source only — Apache-2.0, MIT, or BSD — unless the owner approves an exception in writing.
4. Maintenance and vulnerability check: inspect release cadence and run `dotnet list CafeHub.Backend.sln package --vulnerable --include-transitive`; unresolved critical/high findings block the request.
5. Owner approval in the PR conversation.
6. A pull request that changes `Directory.Packages.props` (together with the first usage, or alone if pre-approving) — never a version change in a `.csproj`.

## Installed baseline

The table below is generated from the real `Directory.Packages.props` and must be kept identical to it.

| Package | Version | Purpose |
|---|---|---|
| Microsoft.EntityFrameworkCore | 10.0.13 | EF Core runtime for MasterDbContext and TenantDbContext |
| Microsoft.EntityFrameworkCore.Design | 10.0.13 | EF Core design-time tooling for migrations (WebApi host) |
| Microsoft.Extensions.Configuration | 10.0.13 | Configuration building exercised in unit tests |
| Microsoft.Extensions.Configuration.Abstractions | 10.0.13 | `IConfiguration` consumption in Infrastructure |
| Microsoft.Extensions.DependencyInjection | 10.0.13 | DI container exercised in unit tests |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.13 | `IServiceCollection` registration surface (Application, Infrastructure) |
| Microsoft.NET.Test.Sdk | 17.14.1 | xUnit test runner platform |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | PostgreSQL database provider for EF Core |
| xunit | 2.9.3 | Unit test framework |
| xunit.runner.visualstudio | 3.1.4 | xUnit Visual Studio test adapter |

## Approved, not yet installed

- **MediatR — exactly 12.5.0.** Version 12.5.0 is the last Apache-2.0 licensed release; from version 13 on, MediatR moved to a commercial license, so **version 13 and above are prohibited**. Floating version ranges such as `12.*` or `*` are prohibited too. MediatR is installed by the Sprint 2 Application Core work (CQRS behaviors), not before.
- **FluentValidation.** Approved for the Sprint 2 validation behaviors; not installed in Sprint 1.

## Prohibited

- SQL Server providers: `Microsoft.Data.SqlClient`, `Microsoft.EntityFrameworkCore.SqlServer`, and any other SQL Server package. PostgreSQL via Npgsql is the only approved database stack (see `architecture/database-schema-decisions.md`).

## Not approved without a documented requirement

Adding any of the following first requires a PRD- or story-backed requirement and owner approval:

- Object mappers: AutoMapper, Mapster.
- Generic repository wrappers.
- Caching packages and message-broker packages.
- Job schedulers: Hangfire, Quartz.
- Payment, AI, printing, or POS provider SDKs.

## Testing dependencies

- xUnit is the only test framework.
- No mocking library until a story justifies one; hand-written fakes are the default (see `architecture/architecture-decisions.md`).

## EF tooling

- Each developer's global `dotnet-ef` tool must be the same version as, or newer than, the EF Core runtime packages. Install or update with `dotnet tool install --global dotnet-ef` / `dotnet tool update --global dotnet-ef`.
- `Microsoft.EntityFrameworkCore.Design` is kept at the exact same version as the EF Core runtime package so migrations built with the tool match the runtime.

## Updates

- If an automated dependency updater (Dependabot or Renovate) is ever enabled, it must be configured to ignore MediatR versions 13 and above.
