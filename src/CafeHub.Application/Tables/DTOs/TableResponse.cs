using System.Text.Json.Serialization;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Tables.DTOs
{
    /// <summary>
    /// The table read model returned to a tenant user: identity, owning branch, number, optional label,
    /// status, timestamps, the QR code (for callers who may manage tables), and an opaque concurrency token.
    /// The QR code is omitted from serialized output when null so staff without the permission cannot scrape it.
    /// </summary>
    public sealed class TableResponse
    {
        public required Guid Id { get; init; }

        public required Guid BranchId { get; init; }

        public required int Number { get; init; }

        public required string? Label { get; init; }

        public required string Status { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required string? QrCode { get; init; }

        public required DateTimeOffset CreatedAt { get; init; }

        public required DateTimeOffset UpdatedAt { get; init; }

        public required string ConcurrencyToken { get; init; }

        public static TableResponse From(BranchTable table, string concurrencyToken, string? qrCode = null)
        {
            ArgumentNullException.ThrowIfNull(table);
            ArgumentNullException.ThrowIfNull(concurrencyToken);

            return new TableResponse
            {
                Id = table.Id,
                BranchId = table.BranchId,
                Number = table.Number,
                Label = table.Label,
                Status = table.Status.ToString(),
                QrCode = qrCode,
                CreatedAt = table.CreatedAt,
                UpdatedAt = table.UpdatedAt,
                ConcurrencyToken = concurrencyToken,
            };
        }
    }

    /// <summary>
    /// The result of a bulk table action: how many tables were affected and the inclusive number range.
    /// </summary>
    public sealed record TableBulkResult(int Count, int FromNumber, int ToNumber);
}
