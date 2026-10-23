# Route and Versioning Conventions

> **Status: Proposed, pending owner confirmation.** The owner confirms this document in the PR
> conversation (Feature/250_SA_ApiVersioningAndRouteConventions). Until that confirmation nothing
> below is active or agreed; this sub-task adds documentation only — no code, middleware,
> attribute, or route changes of any kind.

## Owner confirmation

- Status: Proposed, pending owner confirmation until the owner confirms in the PR conversation.
- On confirmation the status line above is updated in a follow-up commit and the same decision
  text is mirrored in `architecture/architecture-decisions.md` and the TBD register in
  `architecture/database-schema-decisions.md`.

## Purpose

This document proposes the single route and versioning shape for the CafeHub HTTP surface. The
PRD leaves the final route naming open ("PRD section 10.4 API Behavior" requires a defined
versioning strategy and tenant and branch resolution behavior before production sign-off, and
explicitly does not lock the final endpoint route naming — verified against the local PRD on
2026-10-23). Every specific convention below is therefore a proposal for the owner to confirm,
with its rejected alternative stated alongside.

## Proposed route template

- Platform endpoints (tenant-independent): `/api/v1/{resource}`
- Tenant-scoped endpoints (one cafe business): `/t/{slug}/api/v1/{resource}`

The version segment `v1` is a path segment in both families. Platform routes carry it directly
after `/api`; tenant-scoped routes carry the tenant prefix first, then the version, e.g.
`/t/{slug}/api/v1/{resource}`.

*Proposed, pending owner confirmation* — reason: the PRD requires a versioning strategy but
does not lock route naming, and the MVP owning decision for the tenant path prefix is fixed
(owner decision of 2026-10-19); the placement of `/api` and the exact spelling of `v1` above
were chosen to keep versions visible in shared URLs. Rejected alternative: carrying the version
outside the path (header or query string) — see "Rejected alternatives".

## Examples (placeholder resource names only)

The resource names below are placeholders for shape examples; they are illustrative only and
approve no endpoint and no module surface (PRD section 10.4: example endpoint paths in
engineering notes are not final route naming).

| Family | Template | Example |
|---|---|---|
| Platform (tenant-independent) | `/api/v1/{resource}` | `/api/v1/plans` |
| Platform (tenant-independent) | `/api/v1/{resource}/{id}` | `/api/v1/provisioning-requests/7f3b` |
| Tenant-scoped (one cafe) | `/t/{slug}/api/v1/{resource}` | `/t/acme/api/v1/orders` |
| Tenant-scoped (one cafe) | `/t/{slug}/api/v1/{resource}/{id}` | `/t/acme/api/v1/tables/2a9f` |

## Version segment rules

*Proposed, pending owner confirmation* — reason: the AC requires documenting when a new major
version is created; the concrete rule is a convention choice.

- The version is exactly one path segment in the form `v{major}` (`v1` today).
- A new major version is created only for breaking contract changes: removed or renamed members,
  changed request or response shapes, changed status-code mapping, changed authentication
  requirements. The old version stays available side by side (`/api/v1/...` next to
  `/api/v2/...`) during a transition window decided later.
- Non-breaking additions (new optional members, new endpoints) stay inside `v1`.
- There is no unversioned surface: every route contains a version segment. Rejected alternative:
  defaulting unversioned routes to the latest version — invisible tracking of version drift.

## `{slug}` handling

*Proposed, pending owner confirmation* — reason: the AC fixes that this document describes no
resolution code; the failure mode here records existing rules, not new decisions.

- `/t/{slug}` marks a request as tenant-scoped; `{slug}` is a placeholder for the per-tenant
  slug verified against the tenant registry at request time (expected in the future
  tenant-resolution sub-task).
- **This document describes no resolution code.** No middleware, attribute, or resolution logic
  is added here; the MVP tenant identifier source decision is recorded in
  `architecture/architecture-decisions.md`.
- Until validated, `{slug}` is an untrusted path value: an unknown, inactive, or unmatched slug
  must fail closed with the documented safe error (PRD section 10.2: a caller-supplied tenant
  identifier alone is never proof of authorization), and logged correlation must never leak
  secret values.

## Reserved prefix rule

*Proposed, pending owner confirmation* — reason: required by the story AC; the specific reserved
token is a convention choice.

- `/t/` is reserved for the tenant prefix only. Platform routes must never begin with `/t/`.
- Rejected alternative: allowing platform resources under `/t/` — collides structurally with
  tenant routes and hides which surface a request targets.

## Resource naming rules

*Proposed, pending owner confirmation* — reason: the AC requires naming rules; the exact word
form is a convention choice.

- Collection resources are plural, lowercase, kebab-case nouns: `orders`, `provisioning-requests`.
- No verbs in collection paths; actions that are not CRUD-able as resources are sub-resources
  (for example `/orders/{id}/events`) — naming examples stay placeholders.
- Rejected alternative: singular camelCase resources (`/api/v1/orderList`) — inconsistent with
  kebab-case URL readability and upgrades wording asymmetry.

## OpenAPI grouping

*Proposed, pending owner confirmation* — reason: how the two families appear in the document is
a convention choice; the document-level facts are verified behavior of CFMD-249.

- The built-in OpenAPI document (`/openapi/v1.json`, Development-only per CFMD-249) is a single
  document; it currently reports `"paths": { }` because no endpoint exists.
- When endpoints arrive, both families share this one document; callers distinguish them by the
  documented path prefixes (`/api/v1/...` platform, `/t/{slug}/api/v1/...` tenant-scoped).
- Rejected alternative: splitting platform and tenant-scoped routes into two OpenAPI documents —
  two URLs to publish and align; a single contract document is easier to review now. A later
  owner decision may still split documents if shapes diverge.
- How `{slug}` examples appear in OpenAPI: tenant paths are templated with `{slug}` as both a
  documented parameter and a validation obligation; no tenant value is ever baked into the
  document.

## Rejected alternatives

| Alternative | Status | Reason |
|---|---|---|
| Header-based API version (`X-Version: 2`) | Proposed, rejected | Versions leave the URL; deep links, caches, and shared snippets lose the version they pin. |
| Query-string API version (`?api-version=2`) | Proposed, rejected | Same discovery problem: URLs of differing versions collide on the same path and cache keys fragment. |
| Subdomain tenant identification (`{slug}.api.example.com`) | Not chosen for the MVP by owner decision of 2026-10-19 (not a new decision) | Recorded here so the route template and the MVP decision read consistently; TLS/wildcard/certificate concerns ruled it out. |
| Header-based tenant identification (`X-Tenant-Slug`) | Not chosen for the MVP by owner decision of 2026-10-19 (not a new decision) | Same: the MVP owns the path prefix; headers keep tenants invisible in URLs and logs. |

- For header and subdomain tenant identification: described only as "not chosen for the MVP by
  owner decision of 2026-10-19"; this document decides nothing about them.
