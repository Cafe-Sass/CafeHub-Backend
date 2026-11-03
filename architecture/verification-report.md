# Verification report

Story CFMD-353 (BE - Delivery and Handover), Epic CFMD-318.

All checks below are manual checks. The owner supplies real results; this agent never invents or upgrades a result. Every check without an owner-supplied result in this prompt run stays **Not Run**.

## Manual check results

| Check | Status | Date | Observed result | Ran by |
|---|---|---|---|---|
| Master migrations applied twice | Not Run |  |  |  |
| Admin login, refresh, logout, lockout, and `me` | Not Run |  |  |  |
| Plan creation | Not Run |  |  |  |
| Owner registration, payment, approval | Not Run |  |  |  |
| Provisioning run twice with no duplicates | Not Run |  |  |  |
| Tenant database content (schema, role, seeded Owner, settings row) | Not Run |  |  |  |
| Status endpoint | Not Run |  |  |  |
| Tenant owner login, refresh, logout, and `me` | Not Run |  |  |  |
| Cafe profile get, update, and stale update | Not Run |  |  |  |
| Branches create, list, update, lifecycle | Not Run |  |  |  |
| Cross-tenant attempts (token of cafe A on cafe B) | Not Run |  |  |  |
| Prompt 11 isolation script | Not Run |  |  |  |
| Security headers, HTTPS redirect, CORS, and rate limits | Not Run |  |  |  |
| Secret scanning run | Not Run |  |  |  |
| Dependency audit run | Not Run |  |  |  |

## Unit test results (automated, executed)

| Suite | Status | Date | Result | Ran by |
|---|---|---|---|---|
| `dotnet test tests/CafeHub.UnitTests/CafeHub.UnitTests.csproj --configuration Release` | Passed | 2026-11-03 | 1834 passed, 0 failed, 0 skipped | Agent |

## Build and format results (automated, executed)

| Check | Status | Date | Result | Ran by |
|---|---|---|---|---|
| `dotnet build CafeHub.Backend.sln --no-restore --configuration Release` | Passed | 2026-11-03 | 0 errors | Agent |
| `dotnet format CafeHub.Backend.sln --verify-no-changes --no-restore` | Passed | 2026-11-03 | No changes required | Agent |
| `git diff --check` | Passed | 2026-11-03 | Clean | Agent |

## Notes

- All manual checks are **Not Run** because no owner-supplied results were provided in this prompt run. Per the acceptance criteria, every check without an owner-supplied result stays Not Run; none are marked Passed or Failed.
- CI was not monitored or polled for any PR in this story; CI results are unverified.
- PostgreSQL and a running host are not available in this environment, so all database/host-dependent manual checks are Not Run.
