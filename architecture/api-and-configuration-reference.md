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
| GET | `t/{slug}/api/v1/me` | `TenantUser` (`[TenantAuthorize(TenantPolicies.TenantUser)]`) | Current tenant user profile | TenantMeController.cs:24 |
| GET | `t/{slug}/api/v1/cafe-profile` | `TenantUser` | Get cafe profile | TenantCafeProfileController.cs:25 |
| PUT | `t/{slug}/api/v1/cafe-profile` | `TenantOwner` (`[TenantAuthorize(TenantPolicies.TenantOwner)]`) | Update cafe profile | TenantCafeProfileController.cs:39 |
| GET | `t/{slug}/api/v1/branches` | `TenantUser` | List branches (paginated, optional status filter) | TenantBranchesController.cs:26 |
| GET | `t/{slug}/api/v1/branches/{branchId:guid}` | `TenantUser` | Get one branch | TenantBranchesController.cs:42 |
| POST | `t/{slug}/api/v1/branches` | `TenantOwner` | Create a branch (201 + Location header) | TenantBranchesController.cs:58 |
| PUT | `t/{slug}/api/v1/branches/{branchId:guid}` | `TenantOwner` | Update a branch | TenantBranchesController.cs:80 |
| POST | `t/{slug}/api/v1/branches/{branchId:guid}/suspend` | `TenantOwner` | Suspend a branch | TenantBranchesController.cs:105 |
| POST | `t/{slug}/api/v1/branches/{branchId:guid}/reactivate` | `TenantOwner` | Reactivate a branch | TenantBranchesController.cs:124 |
| POST | `t/{slug}/api/v1/branches/{branchId:guid}/retire` | `TenantOwner` | Retire a branch | TenantBranchesController.cs:143 |
| GET | `health/live` | Anonymous | Liveness probe; no dependency checks | Health check middleware (Program.cs:62) |
| GET | `health/ready` | Anonymous | Readiness probe; Master database connectivity | Health check middleware (Program.cs:68) |

### Policy names

| Policy | Defined in | Meaning |
|---|---|---|
| `PlatformAdmin` | `CafeHub.WebApi.Authorization.PlatformAuthorizationPolicies` | Platform administrator |
| `SuperAdmin` | `CafeHub.WebApi.Authorization.PlatformAuthorizationPolicies` | Super administrator |
| `TenantUser` | `CafeHub.WebApi.Authorization.TenantPolicies` | Authenticated tenant user (owner or staff) |
| `TenantOwner` | `CafeHub.WebApi.Authorization.TenantPolicies` | Tenant owner only |

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
| `Cors` | `AllowedOrigins` | CORS allowed origins | `[]` | No |
| `Cors` | `AllowedMethods` | CORS allowed methods | GET, POST, PUT, PATCH | No |
| `Cors` | `AllowedHeaders` | CORS allowed headers | Authorization, Content-Type, Idempotency-Key, X-Correlation-Id | No |
| `Cors` | `ExposedHeaders` | CORS exposed headers | X-Correlation-Id, Retry-After | No |
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
