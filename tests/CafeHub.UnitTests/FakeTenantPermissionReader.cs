using CafeHub.Application.Identity;

namespace CafeHub.UnitTests
{
    internal sealed class FakeTenantPermissionReader : ITenantPermissionReader
    {
        public IReadOnlyList<string> Codes { get; set; } = [];
        public int Reads { get; private set; }
        public Guid? ExpectedBranchId { get; private set; }

        public Task<IReadOnlyList<string>> ReadAsync(Guid userId, CancellationToken cancellationToken, Guid? expectedBranchId = null)
        {
            Reads++;
            ExpectedBranchId = expectedBranchId;
            return Task.FromResult(Codes);
        }
    }
}
