using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Identity
{
    public interface ISuperAdminSeeder
    {
        Task<Result<SeedOutcome>> SeedAsync(CancellationToken cancellationToken);
    }
}
