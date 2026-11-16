# API and configuration reference

All endpoint rows and all configuration rows are traceable to a file and line in the repository. Endpoints were enumerated by grepping the controllers in `src/CafeHub.WebApi/Controllers` for `[Route]`, `[Http...]`, `[TenantAuthorize]`, `[Authorize]`, and `[AllowAnonymous]`. Configuration keys were enumerated by reading `src/CafeHub.WebApi/appsettings.template.json` and every options binder in `src/CafeHub.Infrastructure` and `src/CafeHub.Application`.

## Endpoint reference

| Method | Route | Policy / anonymous | Purpose | Controller (file:line) |
|---|---|---|---|---|
| POST | `api/v1/admin/auth/login` | Anonymous (`[AllowAnonymous]`) | Admin login (issues token) | AdminAuthController.cs:27 |
| POST | `api/v1/admin/auth/refresh` | Anonymous | Admin token refresh | AdminAuthController.cs:41 |
| POST | `api/v1/admin/auth/logout` | Anonymous | Admin logout | AdminAuthController.cs:55 |
| GET | `api/v1/admin/me` | `SuperAdmin` (`[Authorize(Policy = PlatformAuthorizationPolicies.SuperAdmin)]`) | Current admin profile | AdminController.cs:25 |
| GET | `api/v1/admin/plans` | `SuperAdmin` | List plans (paginated) | AdminPlansController.cs:60 |
| GET | `api/v1/admin/plans/{planId:guid}` | `SuperAdmin` | Get one plan | AdminPlansController.cs:76 |
| POST | `api/v1/admin/plans` | `SuperAdmin` | Create a plan | AdminPlansController.cs:27 |
| POST | `api/v1/admin/plans/{planId:guid}/versions` | `SuperAdmin` | Create a plan version | AdminPlansController.cs:43 |
| GET | `api/v1/admin/onboarding-requests` | `SuperAdmin` | List onboarding requests (paginated) | AdminOnboardingRequestsController.cs:27 |
| GET | `api/v1/admin/onboarding-requests/{id:guid}` | `SuperAdmin` | Get one onboarding request | AdminOnboardingRequestsController.cs:43 |
| POST | `api/v1/admin/onboarding-requests/{id:guid}/approve` | `SuperAdmin` | Approve an onboarding request | AdminOnboardingRequestsController.cs:59 |
| POST | `api/v1/admin/onboarding-requests/{id:guid}/reject` | `SuperAdmin` | Reject an onboarding request | AdminOnboardingRequestsController.cs:75 |
| POST | `api/v1/onboarding/requests` | Anonymous (`[AllowAnonymous]`) | Register an owner | OnboardingRequestsController.cs:32 |
| POST | `api/v1/onboarding/requests/{id}/payments` | Anonymous | Pay for an onboarding request | OnboardingRequestsController.cs:51 |
| GET | `api/v1/onboarding/requests/{id}/status` | Anonymous | Check onboarding status | OnboardingRequestsController.cs:72 |
| POST | `t/{slug}/api/v1/auth/login` | Anonymous (`[AllowAnonymous]`) | Tenant owner/login | TenantAuthController.cs:27 |
| POST | `t/{slug}/api/v1/auth/refresh` | Anonymous | Tenant token refresh | TenantAuthController.cs:36 |
| POST | `t/{slug}/api/v1/auth/logout` | Anonymous | Tenant logout | TenantAuthController.cs:45 |
| GET | `t/{slug}/api/v1/me` | `TenantUser` | Current profile, role names, nullable branch ID and effective permissions | TenantMeController.cs |
| GET | `t/{slug}/api/v1/cafe-profile` | `TenantUser` | Get cafe profile | TenantCafeProfileController.cs:25 |
| PUT | `t/{slug}/api/v1/cafe-profile` | `cafe-profile.manage` (Owner) | Update cafe profile | TenantCafeProfileController.cs |
| GET | `t/{slug}/api/v1/branches` | `branches.read` | List branches. Staff see only their assigned active branch. | TenantBranchesController.cs |
| GET | `t/{slug}/api/v1/branches/{branchId:guid}` | `branches.read` | Get one branch. Staff see only their assigned active branch. | TenantBranchesController.cs |
| POST | `t/{slug}/api/v1/branches` | `branches.manage` (Owner) | Create a branch (201 + Location header) | TenantBranchesController.cs |
| PUT | `t/{slug}/api/v1/branches/{branchId:guid}` | `branches.manage` (Owner) | Update a branch | TenantBranchesController.cs |
| POST | `t/{slug}/api/v1/branches/{branchId:guid}/suspend` | `branches.manage` (Owner) | Suspend a branch | TenantBranchesController.cs |
| POST | `t/{slug}/api/v1/branches/{branchId:guid}/reactivate` | `branches.manage` (Owner) | Reactivate a branch | TenantBranchesController.cs |
| POST | `t/{slug}/api/v1/branches/{branchId:guid}/retire` | `branches.manage` (Owner) | Retire a branch. Returns 409 while active staff are assigned. | TenantBranchesController.cs |
| POST | `t/{slug}/api/v1/branches/{branchId:guid}/stations` | `stations.manage` | Create a station in an active branch (201 + Location header). | TenantStationsController.cs |
| GET | `t/{slug}/api/v1/branches/{branchId:guid}/stations` | `TenantUser` | List a branch's stations, ordered by sort order, name, id. | TenantStationsController.cs |
| GET | `t/{slug}/api/v1/branches/{branchId:guid}/stations/{stationId:guid}` | `TenantUser` | Get one station within the caller's branch scope. | TenantStationsController.cs |
| PUT | `t/{slug}/api/v1/branches/{branchId:guid}/stations/{stationId:guid}` | `stations.manage` | Update a station's name and sort order. | TenantStationsController.cs |
| POST | `t/{slug}/api/v1/branches/{branchId:guid}/stations/{stationId:guid}/deactivate` | `stations.manage` | Deactivate a station (row kept). | TenantStationsController.cs |
| POST | `t/{slug}/api/v1/branches/{branchId:guid}/stations/{stationId:guid}/reactivate` | `stations.manage` | Reactivate a station; fails with 409 if an active station already uses the name. | TenantStationsController.cs |
| PUT | `t/{slug}/api/v1/staff/{staffId:guid}/stations` | `stations.manage` | Replace a staff member's station assignments. | TenantStaffController.cs |
| POST | `t/{slug}/api/v1/branches/{branchId:guid}/tables` | `tables.manage` | Add one table to an active branch (201 + Location header). | TenantTablesController.cs |
| POST | `t/{slug}/api/v1/branches/{branchId:guid}/tables/bulk-add` | `tables.manage` | Add N tables (1 to 200) numbered after the highest active number. | TenantTablesController.cs |
| POST | `t/{slug}/api/v1/branches/{branchId:guid}/tables/bulk-deactivate-highest` | `tables.manage` | Deactivate the highest N active tables. | TenantTablesController.cs |
| GET | `t/{slug}/api/v1/branches/{branchId:guid}/tables` | `TenantUser` | List a branch's tables, ordered by number, with optional status and number-range filters. | TenantTablesController.cs |
| GET | `t/{slug}/api/v1/branches/{branchId:guid}/tables/{tableId:guid}` | `TenantUser` | Get one table within the caller's branch scope. | TenantTablesController.cs |
| PUT | `t/{slug}/api/v1/branches/{branchId:guid}/tables/{tableId:guid}` | `tables.manage` | Update a table's label. | TenantTablesController.cs |
| POST | `t/{slug}/api/v1/branches/{branchId:guid}/tables/{tableId:guid}/deactivate` | `tables.manage` | Deactivate a table (row kept). | TenantTablesController.cs |
| POST | `t/{slug}/api/v1/branches/{branchId:guid}/tables/{tableId:guid}/reactivate` | `tables.manage` | Reactivate a table; fails with 409 if the number is taken. | TenantTablesController.cs |
| GET | `t/{slug}/api/v1/branches/{branchId:guid}/tables/{tableId:guid}/qr` | `tables.manage` | Read a table's current QR link within the requested branch. | TenantTablesController.cs |
| GET | `t/{slug}/api/v1/branches/{branchId:guid}/tables/qr-sheet` | `tables.manage` | Read every active table's QR link in table-number order. An existing empty branch returns an empty array. | TenantTablesController.cs |
| POST | `t/{slug}/api/v1/branches/{branchId:guid}/tables/{tableId:guid}/qr/regenerate` | `tables.manage` | Replace an active table's QR code using the body's `concurrencyToken`. Returns the replacement link with its new token. Stale versions, inactive tables, and QR uniqueness conflicts return 409. | TenantTablesController.cs |
| GET | `t/{slug}/api/v1/guest/qr/{code}` | Anonymous, Global rate limit | Resolve an active cafe, branch, and table. All rejected QR contexts return `404 GuestQr.NotFound`. | TenantGuestQrController.cs |
| POST | `t/{slug}/api/v1/staff` | `TenantOwner` | Create staff in an active branch (201 + Location header). Duplicate tenant email returns 409. | TenantStaffController.cs |
| GET | `t/{slug}/api/v1/staff` | `staff.read` | List staff. Branch scope applies before filters, count and paging. | TenantStaffController.cs |
| GET | `t/{slug}/api/v1/staff/{staffId:guid}` | `staff.read` | Get one staff member within scope. | TenantStaffController.cs |
| PUT | `t/{slug}/api/v1/staff/{staffId:guid}` | `staff.manage` | Update staff within scope. Transfers are Owner-only. | TenantStaffController.cs |
| POST | `t/{slug}/api/v1/staff/{staffId:guid}/deactivate` | `staff.manage` | Deactivate staff within scope and invalidate access and refresh tokens. | TenantStaffController.cs |
| POST | `t/{slug}/api/v1/staff/{staffId:guid}/reactivate` | `staff.manage` | Reactivate staff within scope. A transition requires an active branch and a fresh sign-in. | TenantStaffController.cs |
| POST | `t/{slug}/api/v1/staff/{staffId:guid}/reset-password` | `TenantOwner` | Owner resets a staff member's password (revokes tokens) | TenantStaffController.cs:136 |
| GET | `t/{slug}/api/v1/permissions` | `roles.manage` (Owner) | Read the permission catalog | TenantPermissionsController.cs |
| GET | `t/{slug}/api/v1/role-presets` | `roles.manage` (Owner) | Read suggestions without creating roles or grants | TenantPermissionsController.cs |
| GET | `t/{slug}/api/v1/roles` | `roles.manage` (Owner) | List active roles | TenantRolesController.cs |
| GET | `t/{slug}/api/v1/roles/{roleId:guid}` | `roles.manage` (Owner) | Read an active role, its permissions, and its concurrency token from one snapshot | TenantRolesController.cs |
| POST | `t/{slug}/api/v1/roles` | `roles.manage` (Owner) | Create a role from `name` and `permissionCodes` | TenantRolesController.cs |
| PUT | `t/{slug}/api/v1/roles/{roleId:guid}` | `roles.manage` (Owner) | Replace the role name and permissions using `concurrencyToken`. Stale token returns 409. | TenantRolesController.cs |
| DELETE | `t/{slug}/api/v1/roles/{roleId:guid}` | `roles.manage` (Owner) | Retire an unused role (204). Any membership, including deactivated staff, returns 409. | TenantRolesController.cs |
| PUT | `t/{slug}/api/v1/staff/{staffId:guid}/roles` | `roles.manage` (Owner) | Replace `roleIds`. Empty array clears memberships. | TenantStaffController.cs |
| PUT | `t/{slug}/api/v1/staff/{staffId:guid}/permissions` | `roles.manage` (Owner) | Replace personal `permissionCodes`. Empty array clears personal grants only. | TenantStaffController.cs |
| GET | `t/{slug}/api/v1/staff/{staffId:guid}/effective-permissions` | `roles.manage` (Owner) | Read effective permissions. Deactivated staff and staff without an active assigned branch return an empty array. | TenantStaffController.cs |
| POST | `t/{slug}/api/v1/me/password` | `TenantUser` | Staff member changes their own password (revokes tokens) | TenantMeController.cs:40 |
| GET | `health/live` | Anonymous | Liveness probe; no dependency checks | Health check middleware (Program.cs:62) |
| GET | `health/ready` | Anonymous | Readiness probe; Master database connectivity | Health check middleware (Program.cs:68) |

The management QR response contains `tableId`, `number`, `label`, `path`, `url`, and `concurrencyToken`. The path is `/t/{slug}/q/{code}` and the URL uses `Public:AppBaseUrl`. Regeneration takes the current token and replaces the code at commit. The guest resolver returns `cafeName`, `branchId`, `branchName`, `tableId`, and `tableNumber`. Missing and malformed code paths use the same rate limit and `404 GuestQr.NotFound` contract. QR responses use `Cache-Control: no-store`. Allowed origins can read `Retry-After` on a `429` response.

### Policy names

| Policy | Defined in | Meaning |
|---|---|---|
| `PlatformAdmin` | `CafeHub.WebApi.Authorization.PlatformAuthorizationPolicies` | Platform administrator |
| `SuperAdmin` | `CafeHub.WebApi.Authorization.PlatformAuthorizationPolicies` | Super administrator |
| `TenantUser` | `CafeHub.WebApi.Authorization.TenantPolicies` | Authenticated tenant user (owner or staff) |
| `TenantOwner` | `CafeHub.WebApi.Authorization.TenantPolicies` | Tenant owner only |
| `staff.read`, `staff.manage`, `branches.read`, `tables.manage`, `stations.manage` | Tenant permission policy | Owner, or staff with the exact grant and an active assigned branch |
| `roles.manage`, `cafe-profile.manage`, `branches.manage` | Tenant permission policy | Reserved for the Owner. These catalog codes are excluded from staff grants and effective permissions. |

Tenant policies read current grants from the tenant database. The grant query verifies that the staff member still belongs to the active branch captured at authentication and used to scope the request. Missing credentials and permissions return generic 401 and 403 responses. Out-of-scope resources return the same 404 as missing resources. Suspended branches block delegated staff operations and branch reads, while active accounts retain sign-in, refresh, cafe-profile reads, and self-service.

## Configuration reference

| Section | Key | Meaning | Default | Secret? |
|---|---|---|---|---|
| `ConnectionStrings` | `Master` | Master database connection string | (unset) | Yes |
| `ConnectionStrings` | `TenantDesignTime` | Tenant design-time connection string (tooling only) | (unset) | Yes |
| `TenantDatabases` | `Host` | PostgreSQL host for tenant databases | (unset) | No |
| `TenantDatabases` | `Port` | PostgreSQL port | `5432` | No |
| `TenantDatabases` | `SslMode` | PostgreSQL SSL mode | `Prefer` | No |
| `TenantDatabases` | `MaxPoolSize` | PostgreSQL max pool size | `10` | No |
| `OpenApi` | `ExposeOutsideDevelopment` | Expose OpenAPI document outside Development | `false` | No |
| `Jwt` | `Issuer` | JWT issuer | (unset) | No |
| `Jwt` | `Audience` | JWT audience | (unset) | No |
| `Jwt` | `SigningKey` | JWT signing key (must be >= 32 bytes UTF-8) | (unset) | Yes |
| `Jwt` | `AccessTokenLifetimeMinutes` | Platform admin access token lifetime | `15` | No |
| `Jwt` | `ClockSkewSeconds` | JWT clock skew tolerance | `30` | No |
| `Jwt:Tenant` | `Audience` | Tenant JWT audience | (unset) | No |
| `Jwt:Tenant` | `AccessTokenLifetimeMinutes` | Tenant access token lifetime | `15` | No |
| `RefreshTokens` | `LifetimeDays` | Refresh token lifetime | `7` | No |
| `RateLimiting:AdminAuth` | `PermitLimit` | Admin auth rate limit permits | `10` | No |
| `RateLimiting:AdminAuth` | `WindowSeconds` | Admin auth rate limit window | `60` | No |
| `RateLimiting:Global` | `PermitLimit` | Global rate limit permits | `300` | No |
| `RateLimiting:Global` | `WindowSeconds` | Global rate limit window | `60` | No |
| `Public` | `AppBaseUrl` | Required public app origin for printed QR links. HTTPS with no credentials, path prefix, query, or fragment. Development also accepts HTTP loopback. Startup validates this setting. | Required | No |
| `Cors` | `AllowedOrigins` | CORS allowed origins | `[]` | No |
| `Cors` | `AllowedMethods` | CORS allowed methods. Explicit deployment overrides must include `DELETE` for cross-origin role retirement. | GET, POST, PUT, PATCH, DELETE | No |
| `Cors` | `AllowedHeaders` | CORS allowed headers | Authorization, Content-Type, Idempotency-Key, X-Correlation-Id | No |
| `Cors` | `ExposedHeaders` | CORS exposed headers. The policy always exposes `Retry-After` to allowed origins. | X-Correlation-Id, Retry-After | No |
| `Cors` | `AllowCredentials` | CORS allow credentials | `false` | No |
| `Cors` | `MaxAgeSeconds` | CORS max age | `600` | No |
| `RequestLimits` | `MaxBodyBytes` | Max request body size | `1048576` | No |
| `RequestLimits` | `MaxHeadersTotalBytes` | Max request headers size | `32768` | No |
| `Transport:Hsts` | `MaxAgeDays` | HSTS max age | `365` | No |
| `Transport:Hsts` | `IncludeSubDomains` | HSTS include subdomains | `false` | No |
| `Transport:Hsts` | `Preload` | HSTS preload | `false` | No |
| `Transport` | `HttpsPort` | HTTPS port | (null) | No |
| `Transport:ForwardedHeaders` | `Enabled` | Forwarded headers enabled | `false` | No |
| `Transport:ForwardedHeaders` | `KnownProxies` | Known proxies | `[]` | No |
| `Transport:ForwardedHeaders` | `KnownNetworks` | Known networks | `[]` | No |
| `Transport:ForwardedHeaders` | `ForwardLimit` | Forward limit | `1` | No |
| `Pagination` | `DefaultPageSize` | Default page size | `20` | No |
| `Pagination` | `MaxPageSize` | Maximum page size | `100` | No |
| `PlatformSeed` | `SuperAdminEmail` | Super admin seed email | (unset) | Yes |
| `PlatformSeed` | `SuperAdminPassword` | Super admin seed password | (unset) | Yes |
| `Payments` | `Provider` | Payment provider | `Mock` | No |
| `Payments:Mock` | `Outcome` | Mock payment outcome | `Succeed` | No |
| `Payments:Mock` | `AllowOutsideDevelopment` | Allow mock outside Development | `false` | No |

### Secrets

Secret keys (shown by name only; values are placeholders, never committed): `ConnectionStrings:Master`, `ConnectionStrings:TenantDesignTime`, `Jwt:SigningKey`, `PlatformSeed:SuperAdminEmail`, `PlatformSeed:SuperAdminPassword`. Set these via User Secrets locally (`dotnet user-secrets`) and environment variables in deployed environments.

### How each row was verified

- Endpoint table: grep of `src/CafeHub.WebApi/Controllers/*.cs` for `[Route]`, `[HttpGet]`, `[HttpPost]`, `[HttpPut]`, `[TenantAuthorize]`, `[Authorize]`, `[AllowAnonymous]`; route templates and policy names read directly from each action and controller. Health endpoints are mapped in `src/CafeHub.WebApi/Program.cs` via `MapHealthChecks` and verified by unit tests in `tests/CafeHub.UnitTests`.
- Configuration table: `src/CafeHub.WebApi/appsettings.template.json` (all keys and defaults) cross-checked against options binder classes in `src/CafeHub.Infrastructure` and `src/CafeHub.Application` (e.g. `JwtOptionsBinder`, `PaginationOptionsBinder`, `PaginationConfigurationKeys`, `GlobalRateLimitOptionsBinder`, `AuthRateLimitOptionsBinder`, `CorsOptionsBinder`, `RequestLimitsOptionsBinder`, `TransportSecurityOptionsBinder`, `PlatformSeedOptionsBinder`, `PaymentOptionsBinder`, `NotificationOptionsBinder`).
