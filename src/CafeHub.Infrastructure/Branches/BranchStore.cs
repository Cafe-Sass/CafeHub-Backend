using System.Globalization;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Branches;
using CafeHub.Application.Branches.Contracts;
using CafeHub.Application.Branches.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Branches
{
    internal sealed class BranchStore(
        TenantDbContext context,
        ITenantAuditWriter auditWriter,
        TimeProvider timeProvider) : IBranchStore
    {
        public async Task<Result<BranchResponse>> CreateAsync(
            string name,
            string? address,
            string? contactPhone,
            string? openingHoursSummary,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var now = timeProvider.GetUtcNow();
            var branch = Branch.Create(
                Guid.CreateVersion7(),
                name,
                BranchLifecycleStatus.Active,
                address,
                contactPhone,
                openingHoursSummary,
                now,
                actorUserId);

            context.Branches.Add(branch);

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "branches.created.succeeded",
                TargetType = "branches",
                TargetId = branch.Id,
                ActorUserId = actorUserId,
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<BranchResponse>(auditResult.Error!);
            }

            await context.SaveChangesAsync(cancellationToken);

            var token = context.Entry(branch)
                .Property<uint>("xmin")
                .CurrentValue
                .ToString(CultureInfo.InvariantCulture);

            return Result.Success(BranchResponse.From(branch, token));
        }

        public async Task<Result<BranchResponse>> UpdateAsync(
            Guid branchId,
            string name,
            string? address,
            string? contactPhone,
            string? openingHoursSummary,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var branch = await context.Branches
                .FirstOrDefaultAsync(b => b.Id == branchId, cancellationToken);
            if (branch is null)
            {
                return Result.Failure<BranchResponse>(BranchesErrors.NotFound());
            }

            if (!uint.TryParse(concurrencyToken, out var token))
            {
                return Result.Failure<BranchResponse>(
                    ValidationError.Create(new[] { new FieldError("concurrencyToken", "The concurrency token is not a valid unsigned integer.") }));
            }

            context.Entry(branch).Property<uint>("xmin").OriginalValue = token;

            branch.Update(
                name,
                address,
                contactPhone,
                openingHoursSummary,
                timeProvider.GetUtcNow(),
                actorUserId);

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "branches.updated.succeeded",
                TargetType = "branches",
                TargetId = branch.Id,
                ActorUserId = actorUserId,
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<BranchResponse>(auditResult.Error!);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<BranchResponse>(BranchesErrors.ConcurrencyConflict());
            }

            var newToken = context.Entry(branch)
                .Property<uint>("xmin")
                .CurrentValue
                .ToString(CultureInfo.InvariantCulture);

            return Result.Success(BranchResponse.From(branch, newToken));
        }
    }
}
