namespace CafeHub.Application.Staff.DTOs
{
    public sealed class StaffResponse
    {
        public required Guid Id { get; init; }

        public required string FullName { get; init; }

        public required string Email { get; init; }

        public required string? PhoneNumber { get; init; }

        public required Guid BranchId { get; init; }

        public required string BranchName { get; init; }

        public required bool IsActive { get; init; }

        public required DateTimeOffset CreatedAt { get; init; }

        public required DateTimeOffset UpdatedAt { get; init; }

        public required string ConcurrencyToken { get; init; }
    }
}
