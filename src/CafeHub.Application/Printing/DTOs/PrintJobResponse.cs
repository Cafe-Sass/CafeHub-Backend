using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Printing.DTOs
{
    public sealed record PrintJobResponse(
        Guid Id,
        Guid BranchId,
        Guid PrinterId,
        string DocumentType,
        string SourceType,
        Guid SourceId,
        string Status,
        int Attempts,
        int MaxAttempts,
        string? LastErrorCode,
        DateTimeOffset? LeaseExpiresAt,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        string DocumentPayload)
    {
        public static PrintJobResponse From(PrintJob job) =>
            new(
                job.Id,
                job.BranchId,
                job.PrinterId,
                job.DocumentType.ToString(),
                job.SourceType,
                job.SourceId,
                job.Status.ToString(),
                job.Attempts,
                job.MaxAttempts,
                job.LastErrorCode,
                job.LeaseExpiresAt,
                job.CreatedAt,
                job.UpdatedAt,
                job.DocumentPayload);
    }
}
