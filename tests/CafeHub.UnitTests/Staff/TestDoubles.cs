using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
using CafeHub.Application.Staff.Contracts;
using CafeHub.Application.Staff.DTOs;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using Microsoft.AspNetCore.Identity;

namespace CafeHub.UnitTests.Staff
{
    internal sealed class FakeStaffStore : IStaffStore
    {
        public List<CreateStaffCall> Created { get; } = new();
        public List<UpdateStaffCall> Updated { get; } = new();
        public List<Guid> GetByIdRequests { get; } = new();
        public List<StaffListRequest> ListRequests { get; } = new();
        public List<Guid> DeactivateCalls { get; } = new();
        public List<Guid> ReactivateCalls { get; } = new();
        public List<Guid> ResetPasswordCalls { get; } = new();
        public bool ChangePasswordCalled { get; private set; }

        public StaffResponse? CreateResult { get; set; }
        public StaffResponse? UpdateResult { get; set; }
        public StaffResponse? GetByIdResult { get; set; }
        public List<StaffResponse> ListResults { get; } = new();
        public Error? Failure { get; set; }

        public Task<Result<StaffResponse>> CreateAsync(
            string fullName,
            string email,
            string? phoneNumber,
            Guid branchId,
            string rawPassword,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            Created.Add(new CreateStaffCall(fullName, email, phoneNumber, branchId, rawPassword, actorUserId));

            if (Failure is { } failure)
            {
                return Task.FromResult(Result.Failure<StaffResponse>(failure));
            }

            return Task.FromResult(Result.Success(CreateResult ?? MakeResponse(Guid.CreateVersion7(), fullName, email, phoneNumber, branchId)));
        }

        public Task<Result<StaffResponse>> UpdateAsync(
            Guid staffId,
            string fullName,
            string? phoneNumber,
            Guid branchId,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            Updated.Add(new UpdateStaffCall(staffId, fullName, phoneNumber, branchId, concurrencyToken, actorUserId));

            if (Failure is { } failure)
            {
                return Task.FromResult(Result.Failure<StaffResponse>(failure));
            }

            return Task.FromResult(Result.Success(UpdateResult ?? MakeResponse(staffId, fullName, "staff@test.com", phoneNumber, branchId)));
        }

        public Task<StaffResponse?> GetByIdAsync(Guid staffId, CancellationToken cancellationToken)
        {
            GetByIdRequests.Add(staffId);
            return Task.FromResult(GetByIdResult);
        }

        public Task<StaffListPage> ListAsync(StaffListRequest request, CancellationToken cancellationToken)
        {
            ListRequests.Add(request);
            var items = ListResults.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
            var totalCount = request.IncludeTotalCount ? (int?)ListResults.Count : null;
            return Task.FromResult(new StaffListPage(items, totalCount));
        }

        public Task<Result> DeactivateAsync(Guid staffId, Guid actorUserId, CancellationToken cancellationToken)
        {
            DeactivateCalls.Add(staffId);

            if (Failure is { } failure)
            {
                return Task.FromResult(Result.Failure(failure));
            }

            return Task.FromResult(Result.Success());
        }

        public Task<Result> ReactivateAsync(Guid staffId, Guid actorUserId, CancellationToken cancellationToken)
        {
            ReactivateCalls.Add(staffId);

            if (Failure is { } failure)
            {
                return Task.FromResult(Result.Failure(failure));
            }

            return Task.FromResult(Result.Success());
        }

        public Task<Result> ResetPasswordAsync(Guid staffId, string newPassword, Guid actorUserId, CancellationToken cancellationToken)
        {
            ResetPasswordCalls.Add(staffId);

            if (Failure is { } failure)
            {
                return Task.FromResult(Result.Failure(failure));
            }

            return Task.FromResult(Result.Success());
        }

        public Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
        {
            ChangePasswordCalled = true;

            if (Failure is { } failure)
            {
                return Task.FromResult(Result.Failure(failure));
            }

            return Task.FromResult(Result.Success());
        }

        internal static StaffResponse MakeResponse(
            Guid id,
            string fullName,
            string email,
            string? phoneNumber,
            Guid branchId,
            bool isActive = true)
        {
            return new StaffResponse
            {
                Id = id,
                FullName = fullName,
                Email = email,
                PhoneNumber = phoneNumber,
                BranchId = branchId,
                BranchName = "Test Branch",
                IsActive = isActive,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                ConcurrencyToken = "12345",
            };
        }
    }

    internal sealed record CreateStaffCall(
        string FullName,
        string Email,
        string? PhoneNumber,
        Guid BranchId,
        string RawPassword,
        Guid ActorUserId);

    internal sealed record UpdateStaffCall(
        Guid StaffId,
        string FullName,
        string? PhoneNumber,
        Guid BranchId,
        string ConcurrencyToken,
        Guid ActorUserId);

    internal sealed class FakeStaffCurrentTenantUser : ICurrentTenantUser
    {
        public bool IsAuthenticated { get; set; } = true;
        public Guid? UserId { get; set; } = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        public Guid? TenantId { get; set; } = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public IReadOnlyCollection<string> Roles { get; set; } = new[] { "Owner" };
        public Guid? BranchId { get; set; }
    }

    internal sealed class FakeStaffAuditWriter : ITenantAuditWriter
    {
        public List<TenantAuditEntry> Entries { get; } = new();
        public Result Failure { get; set; } = Result.Success();

        public Task<Result> WriteAsync(TenantAuditEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.FromResult(Failure);
        }
    }
}
