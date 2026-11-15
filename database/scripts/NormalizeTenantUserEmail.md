# Tenant email upgrade

1. Stop old tenant API writers and drain in-flight requests.
2. Apply the tenant migration using the upgrade worker with tenant API traffic
   disabled, or use the companion SQL procedure below. The worker computes an
   inventory proof with the sign-in normalizer. The migration checks that proof
   against the locked rows and rejects invalid emails, duplicates, and any
   normalization mismatch before changing the data or index.
3. Verify the migration history entry (`20261115160000_NormalizeTenantUserEmail`),
   the unique `uq_tenant_users_normalized_email` index, and expected account sign-in.
   Start serving tenant traffic on the new API after verification.

## Companion SQL

Export the following query from the target tenant database to a private UTF-8 CSV.
Keep writes paused through the export and migration.

```sql
SELECT id, email, lower(btrim(email)) AS sql_backfill
FROM tenant_users
WHERE user_kind IN ('Owner', 'Staff') OR email IS NOT NULL OR normalized_email IS NOT NULL;
```

Run `pwsh -File database/scripts/Test-TenantEmailBackfill.ps1 -InventoryPath <private-csv>`.
On exit code 0, pass the printed `EmailProof` to
`psql -v email_proof=<proof> -f database/scripts/NormalizeTenantUserEmail.sql`
using the same database. The script checks the proof and records migration
history in one transaction. Remove the private CSV after use.

Resolve any invalid addresses, canonical duplicates, or normalization
mismatch before retrying. Keep account rows and password hashes intact.
Deactivated accounts still reserve their email. The schema downgrade removes
the unique index and retains the normalized data.
