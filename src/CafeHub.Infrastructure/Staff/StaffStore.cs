using System.Globalization;
using System.Text.RegularExpressions;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Staff;
using CafeHub.Application.Staff.Contracts;
using CafeHub.Application.Staff.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

        public StaffStore(
            TenantDbContext context,
            IPasswordHasher<TenantUser> passwordHasher,
            ITenantAuditWriter auditWriter,
            TimeProvider timeProvider)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            this.auditWriter = auditWriter ?? throw new ArgumentNullException(nameof(auditWriter));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
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
            var normalizedEmail = email.ToUpperInvariant();
            var exists = await context.TenantUsers
                .AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
            if (exists)
            {
                return Result.Failure<StaffResponse>(StaffErrors.EmailConflict());
            }

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

            var staffId = Guid.CreateVersion7();
            var userName = GenerateUserName(email, staffId);
            var normalizedUserName = userName.ToUpperInvariant();
            var securityStamp = Guid.NewGuid().ToString("N");
            var concurrencyStamp = Guid.NewGuid().ToString("N");
            var now = timeProvider.GetUtcNow();

            var user = TenantUser.CreateStaff(
                staffId,
                userName,
                normalizedUserName,
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
            }, cancellationToken);

            if (auditResult.IsFailure)
            {
                return Result.Failure<StaffResponse>(auditResult.Error!);
            }

            await context.SaveChangesAsync(cancellationToken);

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
            var user = await context.TenantUsers
                .FirstOrDefaultAsync(u => u.Id == staffId, cancellationToken);
            if (user is null)
            {
                return Result.Failure<StaffResponse>(StaffErrors.NotFound());
            }

            if (user.UserKind != TenantUserKind.Staff)
            {
                return Result.Failure<StaffResponse>(StaffErrors.CannotModifyOwner());
            }

            if (!uint.TryParse(concurrencyToken, out var token))
            {
                return Result.Failure<StaffResponse>(StaffErrors.InvalidConcurrencyToken());
            }

            context.Entry(user).Property<uint>("xmin").OriginalValue = token;

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

            user.UpdateStaffProfile(fullName, phoneNumber, branchId, timeProvider.GetUtcNow(), actorUserId);

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "staff.updated.succeeded",
                TargetType = TargetType,
                TargetId = user.Id,
                ActorUserId = actorUserId,
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

        public async Task<StaffResponse?> GetByIdAsync(Guid staffId, CancellationToken cancellationToken)
        {
            var user = await context.TenantUsers
                .FirstOrDefaultAsync(u => u.Id == staffId, cancellationToken);
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
            var user = await context.TenantUsers
                .FirstOrDefaultAsync(u => u.Id == staffId, cancellationToken);
            if (user is null || user.UserKind != TenantUserKind.Staff)
            {
                return Result.Failure(StaffErrors.NotFound());
            }

            var now = timeProvider.GetUtcNow();
            user.Deactivate(now, actorUserId);

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
            var user = await context.TenantUsers
                .FirstOrDefaultAsync(u => u.Id == staffId, cancellationToken);
            if (user is null || user.UserKind != TenantUserKind.Staff)
            {
                return Result.Failure(StaffErrors.NotFound());
            }

            var now = timeProvider.GetUtcNow();
            user.Reactivate(now, actorUserId);

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "staff.reactivated.succeeded",
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

        public async Task<Result> ResetPasswordAsync(Guid staffId, string newPassword, Guid actorUserId, CancellationToken cancellationToken)
        {
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

        private static string GenerateUserName(string email, Guid userId)
        {
            var prefix = email.Split('@')[0].ToLowerInvariant();
            var sanitized = Regex.Replace(prefix, "[^a-z0-9]", "-").Trim('-');
            if (sanitized.Length < 3)
            {
                sanitized = "user-" + userId.ToString("N")[..8];
            }
            else if (sanitized.Length > 28)
            {
                sanitized = sanitized[..28].Trim('-');
            }

            var suffix = "-" + userId.ToString("N")[..6];
            return sanitized + suffix;
        }

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
