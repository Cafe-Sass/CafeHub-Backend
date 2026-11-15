using System.Security.Cryptography;
using System.Text;
using CafeHub.Application.Identity.Commands;
using CafeHub.Domain.Identity;
using Npgsql;

namespace CafeHub.Infrastructure.Provisioning
{
    internal static class TenantEmailBackfill
    {
        internal const string MigrationId = "20261115160000_NormalizeTenantUserEmail";
        internal const string InitialTenantMigrationId = "20261026020810_InitialTenantSchema";

        internal static async Task PrepareAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
        {
            var proof = await ReadProofAsync(connection, cancellationToken);
            await using var command = new NpgsqlCommand(
                "SELECT set_config('cafehub.tenant_email_backfill_proof', @proof, false)", connection);
            command.Parameters.AddWithValue("proof", proof ?? string.Empty);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task<string?> ReadProofAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
        {
            const string query = """
                SELECT id, email FROM tenant_users
                WHERE user_kind IN ('Owner', 'Staff') OR email IS NOT NULL OR normalized_email IS NOT NULL
                ORDER BY id
                """;
            await using var command = new NpgsqlCommand(query, connection);
            await using var rows = await command.ExecuteReaderAsync(cancellationToken);
            using var proof = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            while (await rows.ReadAsync(cancellationToken))
            {
                var digest = CreateRowDigest(rows.GetGuid(0), rows.IsDBNull(1) ? null : rows.GetString(1));
                if (digest is null)
                {
                    return null;
                }

                proof.AppendData(digest);
            }

            return Convert.ToHexStringLower(proof.GetHashAndReset());
        }

        internal static byte[]? CreateRowDigest(Guid id, string? email)
        {
            if (string.IsNullOrWhiteSpace(email) || email.Length > LoginTenantUserCommandValidator.MaximumEmailLength)
            {
                return null;
            }

            var canonical = TenantUserEmail.Normalize(email);
            var at = canonical.IndexOf('@');
            if (at <= 0 || at == canonical.Length - 1 || at != canonical.LastIndexOf('@'))
            {
                return null;
            }

            // The SQL checks both the raw input and the normalization candidate under the table lock.
            return SHA256.HashData(Encoding.UTF8.GetBytes($"{id:D}{email}\0{canonical}"));
        }
    }
}
