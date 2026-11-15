using System.Globalization;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Staff;
using CafeHub.Application.Staff.Contracts;
using CafeHub.Application.Staff.DTOs;
using CafeHub.Domain.Identity;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Persistence.Tenant;
using CafeHub.Infrastructure.Persistence.Tenant.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Staff
{
    internal sealed class StaffStore : IStaffStore
    {
        private const string RevocationReason = "admin_revoked";
        private const string TargetType = "tenant_users";
        private readonly TenantDbContext context;
        private readonly IPasswordHasher<TenantUser> passwordHasher;
        private readonly ITenantAuditWriter auditWriter;
        private readonly TimeProvider timeProvider;
        private readonly ICurrentTenantUser currentUser;

        public StaffStore(
            TenantDbContext context,
            IPasswordHasher<TenantUser> passwordHasher,
            ITenantAuditWriter auditWriter,
            TimeProvider timeProvider,
            ICurrentTenantUser currentUser)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            this.auditWriter = auditWriter ?? throw new ArgumentNullException(nameof(auditWriter));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            this.currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<Result<StaffResponse>> CreateAsync(
            string fullName,
            string email,
            string? phoneNumber,
            Guid branchId,
            string rawPassword,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            if (currentUser.AccountAccess is not { IsStaff: false })
            {
                return Result.Failure<StaffResponse>(AuthErrors.Forbidden());
            }

            await context.LockBranchesAsync(cancellationToken, branchId);
            var branch = await context.Branches
                .FirstOrDefaultAsync(b => b.Id == branchId, cancellationToken);
            if (branch is null)
            {
                return Result.Failure<StaffResponse>(StaffErrors.BranchNotFound());
            }

            if (branch.LifecycleStatus != BranchLifecycleStatus.Active)
            {
                return Result.Failure<StaffResponse>(StaffErrors.BranchNotActive());
            }

            var normalizedEmail = TenantUserEmail.Normalize(email);
            if (await context.TenantUsers.AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken))
            {
                return Result.Failure<StaffResponse>(StaffErrors.EmailConflict());
            }

            var staffId = Guid.CreateVersion7();
            var userName = GenerateUserName(staffId);
            for (var retry = 0; await context.TenantUsers.AnyAsync(
                user => user.NormalizedUserName.ToLower() == userName, cancellationToken); retry++)
            {
                if (retry == 2)
                {
                    return Result.Failure<StaffResponse>(StaffErrors.ConcurrencyConflict());
                }

                staffId = Guid.CreateVersion7();
                userName = GenerateUserName(staffId);
            }

            var securityStamp = Guid.NewGuid().ToString("N");
            var concurrencyStamp = Guid.NewGuid().ToString("N");
            var now = timeProvider.GetUtcNow();

            var user = TenantUser.CreateStaff(
                staffId,
                userName,
                userName,
                fullName,
                email,
                normalizedEmail,
                phoneNumber,
                "placeholder-hash",
                securityStamp,
                concurrencyStamp,
                branchId,
                now,
                actorUserId);

            var passwordHash = passwordHasher.HashPassword(user, rawPassword);
            user.SetPasswordHash(passwordHash, securityStamp, concurrencyStamp, now, actorUserId);

            context.TenantUsers.Add(user);

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "staff.created.succeeded",
                TargetType = TargetType,
                TargetId = user.Id,
                ActorUserId = actorUserId,
                Changes = TenantAuditChangeSets.StaffCreate(user, branch),
            }, cancellationToken);

            if (auditResult.IsFailure)
            {
                return Result.Failure<StaffResponse>(auditResult.Error!);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation } violation)
            {
                if (violation.ConstraintName == TenantUserConfiguration.UniqueEmailIndex)
                {
                    return Result.Failure<StaffResponse>(StaffErrors.EmailConflict());
                }

                if (violation.ConstraintName is "uq_tenant_users_normalized_user_name" or "pk_tenant_users")
                {
                    return Result.Failure<StaffResponse>(StaffErrors.ConcurrencyConflict());
                }

                throw;
            }

            var token = context.Entry(user)
                .Property<uint>("xmin")
                .CurrentValue
                .ToString(CultureInfo.InvariantCulture);

            return Result.Success(MapToResponse(user, branch.Name, token));
        }

        public async Task<Result<StaffResponse>> UpdateAsync(
            Guid staffId,
            string fullName,
            string? phoneNumber,
            Guid branchId,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var target = await FindManagedUserAsync(staffId, actorUserId, cancellationToken, branchId);
            if (target.IsFailure)
            {
                return Result.Failure<StaffResponse>(target.Error!);
            }

            var user = target.Value;
            if (user.UserKind != TenantUserKind.Staff)
            {
                return Result.Failure<StaffResponse>(StaffErrors.CannotModifyOwner());
            }

            if (!uint.TryParse(concurrencyToken, out var token))
            {
                return Result.Failure<StaffResponse>(StaffErrors.InvalidConcurrencyToken());
            }

            context.Entry(user).Property<uint>("xmin").OriginalValue = token;

            await context.LockBranchesAsync(cancellationToken, user.BranchId!.Value, branchId);
            var branch = await context.Branches
                .FirstOrDefaultAsync(b => b.Id == branchId, cancellationToken);
            if (branch is null)
            {
                return Result.Failure<StaffResponse>(StaffErrors.BranchNotFound());
            }

            if (branch.LifecycleStatus != BranchLifecycleStatus.Active)
            {
                return Result.Failure<StaffResponse>(StaffErrors.BranchNotActive());
            }

            var changes = TenantAuditChangeSets.StaffUpdate(user, fullName, branchId);

            user.UpdateStaffProfile(fullName, phoneNumber, branchId, timeProvider.GetUtcNow(), actorUserId);

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "staff.updated.succeeded",
                TargetType = TargetType,
                TargetId = user.Id,
                ActorUserId = actorUserId,
                Changes = changes,
            }, cancellationToken);

            if (auditResult.IsFailure)
            {
                return Result.Failure<StaffResponse>(auditResult.Error!);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<StaffResponse>(StaffErrors.ConcurrencyConflict());
            }

            var newToken = context.Entry(user)
                .Property<uint>("xmin")
                .CurrentValue
                .ToString(CultureInfo.InvariantCulture);

            return Result.Success(MapToResponse(user, branch.Name, newToken));
        }

        public async Task<StaffResponse?> GetByIdAsync(Guid staffId, CancellationToken cancellationToken, Guid? branchId = null)
        {
            var user = await context.TenantUsers
                .FirstOrDefaultAsync(u => u.Id == staffId && (branchId == null || u.BranchId == branchId), cancellationToken);
            if (user is null || user.UserKind != TenantUserKind.Staff)
            {
                return null;
            }

            var branchName = string.Empty;
            if (user.BranchId.HasValue)
            {
                branchName = await context.Branches
                    .Where(b => b.Id == user.BranchId.Value)
                    .Select(b => b.Name)
                    .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
            }

            var token = context.Entry(user)
                .Property<uint>("xmin")
                .CurrentValue
                .ToString(CultureInfo.InvariantCulture);

            return MapToResponse(user, branchName, token);
        }

        public async Task<StaffListPage> ListAsync(StaffListRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            IQueryable<TenantUser> query = context.TenantUsers
                .Where(u => u.UserKind == TenantUserKind.Staff);

            if (request.BranchId.HasValue)
            {
                query = query.Where(u => u.BranchId == request.BranchId.Value);
            }

            if (request.IsActive is true)
            {
                query = query.Where(u => u.DeletedAt == null);
            }
            else if (request.IsActive is false)
            {
                query = query.Where(u => u.DeletedAt != null);
            }

            int? totalCount = null;
            if (request.IncludeTotalCount)
            {
                totalCount = await query.CountAsync(cancellationToken);
            }

            var skip = (request.Page - 1) * request.PageSize;
            var fetchedUsers = await query
                .OrderBy(u => u.CreatedAt)
                .Skip(skip)
                .Take(request.PageSize + 1)
                .ToListAsync(cancellationToken);

            var branchIds = fetchedUsers
                .Where(u => u.BranchId.HasValue)
                .Select(u => u.BranchId!.Value)
                .Distinct()
                .ToList();

            var branchNames = await context.Branches
                .Where(b => branchIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id, b => b.Name, cancellationToken);

            var items = fetchedUsers.Select(u =>
            {
                var bName = u.BranchId.HasValue && branchNames.TryGetValue(u.BranchId.Value, out var n)
                    ? n
                    : string.Empty;

                var token = context.Entry(u)
                    .Property<uint>("xmin")
                    .CurrentValue
                    .ToString(CultureInfo.InvariantCulture);

                return MapToResponse(u, bName, token);
            }).ToList();

            return new StaffListPage(items, totalCount);
        }

        public async Task<Result> DeactivateAsync(Guid staffId, Guid actorUserId, CancellationToken cancellationToken)
        {
            var target = await FindManagedUserAsync(staffId, actorUserId, cancellationToken);
            if (target.IsFailure)
            {
                return Result.Failure(target.Error!);
            }

            var user = target.Value;
            if (user.UserKind != TenantUserKind.Staff)
            {
                return Result.Failure(StaffErrors.NotFound());
            }

            await context.LockBranchesAsync(cancellationToken, user.BranchId!.Value);
            var now = timeProvider.GetUtcNow();
            var changes = TenantAuditChangeSets.StaffLifecycleStatus(user, false);
            if (user.DeletedAt is null)
            {
                user.Deactivate(now, actorUserId);
                context.Entry(user).Property(u => u.SecurityStamp).CurrentValue = Guid.NewGuid().ToString("N");
            }

            var activeTokens = await context.TenantRefreshTokens
                .Where(t => t.UserId == staffId && t.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var token in activeTokens)
            {
                token.Revoke(now, RevocationReason);
            }

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "staff.deactivated.succeeded",
                TargetType = TargetType,
                TargetId = user.Id,
                ActorUserId = actorUserId,
                Changes = changes,
            }, cancellationToken);

            if (auditResult.IsFailure)
            {
                return Result.Failure(auditResult.Error!);
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        public async Task<Result> ReactivateAsync(Guid staffId, Guid actorUserId, CancellationToken cancellationToken)
        {
            var target = await FindManagedUserAsync(staffId, actorUserId, cancellationToken);
            if (target.IsFailure)
            {
                return Result.Failure(target.Error!);
            }

            var user = target.Value;
            if (user.UserKind != TenantUserKind.Staff)
            {
                return Result.Failure(StaffErrors.NotFound());
            }

            if (user.DeletedAt is null)
            {
                return Result.Success();
            }

            await context.LockBranchesAsync(cancellationToken, user.BranchId!.Value);
            if (!await context.Branches.AnyAsync(
                branch => branch.Id == user.BranchId && branch.LifecycleStatus == BranchLifecycleStatus.Active,
                cancellationToken))
            {
                return Result.Failure(StaffErrors.ReactivationBranchNotActive());
            }

            var now = timeProvider.GetUtcNow();
            var changes = TenantAuditChangeSets.StaffLifecycleStatus(user, true);
            user.Reactivate(now, actorUserId);

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "staff.reactivated.succeeded",
                TargetType = TargetType,
                TargetId = user.Id,
                ActorUserId = actorUserId,
                Changes = changes,
            }, cancellationToken);

            if (auditResult.IsFailure)
            {
                return Result.Failure(auditResult.Error!);
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        public async Task<Result> ResetPasswordAsync(Guid staffId, string newPassword, Guid actorUserId, CancellationToken cancellationToken)
        {
            if (currentUser.AccountAccess is not { IsStaff: false })
            {
                return Result.Failure(AuthErrors.Forbidden());
            }

            await context.LockUserAsync(staffId, cancellationToken);
            var user = await context.TenantUsers
                .FirstOrDefaultAsync(u => u.Id == staffId, cancellationToken);
            if (user is null || user.UserKind != TenantUserKind.Staff)
            {
                return Result.Failure(StaffErrors.NotFound());
            }

            var now = timeProvider.GetUtcNow();
            var newHash = passwordHasher.HashPassword(user, newPassword);
            var newSecurityStamp = Guid.NewGuid().ToString("N");
            var newConcurrencyStamp = Guid.NewGuid().ToString("N");

            user.SetPasswordHash(newHash, newSecurityStamp, newConcurrencyStamp, now, actorUserId);

            var activeTokens = await context.TenantRefreshTokens
                .Where(t => t.UserId == staffId && t.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var token in activeTokens)
            {
                token.Revoke(now, RevocationReason);
            }

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "staff.password_reset.succeeded",
                TargetType = TargetType,
                TargetId = user.Id,
                ActorUserId = actorUserId,
            }, cancellationToken);

            if (auditResult.IsFailure)
            {
                return Result.Failure(auditResult.Error!);
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        public async Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
        {
            await context.LockUserAsync(userId, cancellationToken);
            var user = await context.TenantUsers
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user is null || user.DeletedAt != null)
            {
                return Result.Failure(StaffErrors.NotFound());
            }

            var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
            if (verification == PasswordVerificationResult.Failed)
            {
                return Result.Failure(StaffErrors.InvalidCurrentPassword());
            }

            var now = timeProvider.GetUtcNow();
            var newHash = passwordHasher.HashPassword(user, newPassword);
            var newSecurityStamp = Guid.NewGuid().ToString("N");
            var newConcurrencyStamp = Guid.NewGuid().ToString("N");

            user.SetPasswordHash(newHash, newSecurityStamp, newConcurrencyStamp, now, userId);

            var activeTokens = await context.TenantRefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var token in activeTokens)
            {
                token.Revoke(now, RevocationReason);
            }

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "staff.password_changed.succeeded",
                TargetType = TargetType,
                TargetId = user.Id,
                ActorUserId = userId,
            }, cancellationToken);

            if (auditResult.IsFailure)
            {
                return Result.Failure(auditResult.Error!);
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        internal static string GenerateUserName(Guid staffId) => $"staff-{staffId:N}";

        private async Task<Result<TenantUser>> FindManagedUserAsync(
            Guid staffId, Guid actorId, CancellationToken cancellationToken, Guid? destinationId = null)
        {
            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<TenantUser>(scope.Error!);
            }

            if (scope.Value is { } branchId)
            {
                if (destinationId.HasValue && destinationId != branchId)
                {
                    return Result.Failure<TenantUser>(StaffErrors.NotFound());
                }

                foreach (var id in new[] { actorId, staffId }.Distinct().Order())
                {
                    if (!await context.LockBranchStaffAsync(id, branchId, cancellationToken))
                    {
                        return Result.Failure<TenantUser>(id == actorId ? AuthErrors.Forbidden() : StaffErrors.NotFound());
                    }
                }

                await context.LockBranchesAsync(cancellationToken, branchId);
                if (!await ActorIsInActiveBranchAsync(actorId, branchId, cancellationToken))
                {
                    return Result.Failure<TenantUser>(AuthErrors.Forbidden());
                }
            }
            else
            {
                await context.LockUserAsync(staffId, cancellationToken);
            }

            var user = await context.TenantUsers.FirstOrDefaultAsync(
                candidate => candidate.Id == staffId && (scope.Value == null || candidate.BranchId == scope.Value),
                cancellationToken);
            return user is null ? Result.Failure<TenantUser>(StaffErrors.NotFound()) : Result.Success(user);
        }

        private Task<bool> ActorIsInActiveBranchAsync(Guid actorId, Guid branchId, CancellationToken cancellationToken) =>
            context.TenantUsers.AnyAsync(
                user => user.Id == actorId && user.BranchId == branchId && user.DeletedAt == null &&
                    context.Branches.Any(branch => branch.Id == branchId && branch.LifecycleStatus == BranchLifecycleStatus.Active),
                cancellationToken);

        private static StaffResponse MapToResponse(TenantUser user, string branchName, string concurrencyToken)
        {
            return new StaffResponse
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                BranchId = user.BranchId ?? Guid.Empty,
                BranchName = branchName,
                IsActive = user.DeletedAt == null,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                ConcurrencyToken = concurrencyToken,
            };
        }
    }
}
