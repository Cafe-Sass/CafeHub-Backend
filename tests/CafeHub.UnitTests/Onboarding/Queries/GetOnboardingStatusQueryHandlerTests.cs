using System.Reflection;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Onboarding;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Onboarding.DTOs;
using CafeHub.Application.Onboarding.Queries;
using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Provisioning;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Onboarding.Queries
{
    public sealed class GetOnboardingStatusQueryHandlerTests
    {
        private sealed class FakeStatusReader : IOnboardingStatusReader
        {
            public OnboardingStatusSnapshot? Snapshot { get; set; }

            public Task<OnboardingStatusSnapshot?> FindByRequestIdAsync(Guid requestId, CancellationToken cancellationToken)
            {
                return Task.FromResult(Snapshot);
            }
        }

        private static readonly Guid RequestId = Guid.CreateVersion7();

        [Fact]
        public async Task UnknownId_ReturnsRequestNotFound()
        {
            var reader = new FakeStatusReader { Snapshot = null };
            var handler = new GetOnboardingStatusQueryHandler(reader);

            var result = await handler.Handle(new GetOnboardingStatusQuery(RequestId), CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(OnboardingErrors.RequestNotFound().Code, result.Error!.Code);
            Assert.Equal(OnboardingErrors.RequestNotFound().Message, result.Error!.Message);
            Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        }

        [Theory]
        [InlineData(OnboardingRequestStatus.AwaitingPayment, OnboardingStatusValues.Registered)]
        [InlineData(OnboardingRequestStatus.PaidAwaitingReview, OnboardingStatusValues.AwaitingReview)]
        [InlineData(OnboardingRequestStatus.Approved, OnboardingStatusValues.Provisioning)]
        [InlineData(OnboardingRequestStatus.Rejected, OnboardingStatusValues.Rejected)]
        public async Task FoundRequest_MapsToSingleStatusValue(
            OnboardingRequestStatus requestStatus,
            string expectedStatus)
        {
            var reader = new FakeStatusReader
            {
                Snapshot = requestStatus == OnboardingRequestStatus.Approved
                    ? new OnboardingStatusSnapshot(requestStatus, TenantLifecycleStatus.Provisioning, ProvisioningJobStatus.Pending)
                    : new OnboardingStatusSnapshot(requestStatus, null, null),
            };
            var handler = new GetOnboardingStatusQueryHandler(reader);

            var result = await handler.Handle(new GetOnboardingStatusQuery(RequestId), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(expectedStatus, result.Value!.Status);
        }

        [Fact]
        public async Task Response_ContainsOnlyTheStatusMember()
        {
            var responseType = typeof(OnboardingStatusResponse);

            var members = responseType.GetMembers()
                .Where(member => member.MemberType is MemberTypes.Property or MemberTypes.Field)
                .Where(member => member.DeclaringType == responseType)
                .Select(member => member.Name)
                .ToList();

            Assert.Equal(new[] { "Status" }, members);
        }
    }
}
