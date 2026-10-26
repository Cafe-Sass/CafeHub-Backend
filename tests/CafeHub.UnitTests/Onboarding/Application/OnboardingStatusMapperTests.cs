using CafeHub.Application.Onboarding;
using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Provisioning;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Onboarding.Application
{
    public sealed class OnboardingStatusMapperTests
    {
        private static Dictionary<(OnboardingRequestStatus RequestStatus, TenantLifecycleStatus? TenantStatus, ProvisioningJobStatus? JobStatus), string> CreateExpectedMap()
        {
            var map = new Dictionary<(OnboardingRequestStatus, TenantLifecycleStatus?, ProvisioningJobStatus?), string>();

            map[(OnboardingRequestStatus.AwaitingPayment, null, null)] = OnboardingStatusValues.Registered;
            map[(OnboardingRequestStatus.PaidAwaitingReview, null, null)] = OnboardingStatusValues.AwaitingReview;
            map[(OnboardingRequestStatus.Rejected, null, null)] = OnboardingStatusValues.Rejected;
            map[(OnboardingRequestStatus.PaymentFailed, null, null)] = OnboardingStatusValues.Failed;
            map[(OnboardingRequestStatus.Cancelled, null, null)] = OnboardingStatusValues.Failed;

            foreach (var jobStatus in new ProvisioningJobStatus?[] { null, ProvisioningJobStatus.Pending, ProvisioningJobStatus.Running, ProvisioningJobStatus.Succeeded })
            {
                map[(OnboardingRequestStatus.Approved, TenantLifecycleStatus.Provisioning, jobStatus)] = OnboardingStatusValues.Provisioning;
            }

            map[(OnboardingRequestStatus.Approved, TenantLifecycleStatus.Provisioning, ProvisioningJobStatus.Failed)] = OnboardingStatusValues.Failed;

            foreach (var tenantStatus in new[] { TenantLifecycleStatus.Active, TenantLifecycleStatus.Suspended, TenantLifecycleStatus.Terminated })
            {
                foreach (var jobStatus in new ProvisioningJobStatus?[] { null, ProvisioningJobStatus.Pending, ProvisioningJobStatus.Running, ProvisioningJobStatus.Succeeded, ProvisioningJobStatus.Failed })
                {
                    map[(OnboardingRequestStatus.Approved, tenantStatus, jobStatus)] = OnboardingStatusValues.Ready;
                }
            }

            return map;
        }

        public static TheoryData<OnboardingRequestStatus, TenantLifecycleStatus?, ProvisioningJobStatus?, string> LegalCombinations()
        {
            var theoryData = new TheoryData<OnboardingRequestStatus, TenantLifecycleStatus?, ProvisioningJobStatus?, string>();
            foreach (var pair in CreateExpectedMap())
            {
                theoryData.Add(pair.Key.RequestStatus, pair.Key.TenantStatus, pair.Key.JobStatus, pair.Value);
            }

            return theoryData;
        }

        [Theory]
        [MemberData(nameof(LegalCombinations))]
        public void Map_ReturnsExactlyOneStatus_ForEveryLegalCombination(
            OnboardingRequestStatus requestStatus,
            TenantLifecycleStatus? tenantStatus,
            ProvisioningJobStatus? jobStatus,
            string expectedStatus)
        {
            var status = OnboardingStatusMapper.Map(requestStatus, tenantStatus, jobStatus);

            Assert.Equal(expectedStatus, status);
        }

        [Fact]
        public void Map_FailsForEveryUnmappedCombination()
        {
            var expectedMap = CreateExpectedMap();
            var requestStatuses = Enum.GetValues<OnboardingRequestStatus>();
            var tenantStatuses = new TenantLifecycleStatus?[] { null, TenantLifecycleStatus.Provisioning, TenantLifecycleStatus.Active, TenantLifecycleStatus.Suspended, TenantLifecycleStatus.Terminated };
            var jobStatuses = new ProvisioningJobStatus?[] { null, ProvisioningJobStatus.Pending, ProvisioningJobStatus.Running, ProvisioningJobStatus.Succeeded, ProvisioningJobStatus.Failed };

            foreach (var requestStatus in requestStatuses)
            {
                foreach (var tenantStatus in tenantStatuses)
                {
                    foreach (var jobStatus in jobStatuses)
                    {
                        if (expectedMap.ContainsKey((requestStatus, tenantStatus, jobStatus)))
                        {
                            continue;
                        }

                        Assert.Throws<InvalidOperationException>(
                            () => OnboardingStatusMapper.Map(requestStatus, tenantStatus, jobStatus));
                    }
                }
            }
        }

        [Fact]
        public void Map_FailsForUndefinedStatusValues()
        {
            Assert.Throws<InvalidOperationException>(
                () => OnboardingStatusMapper.Map((OnboardingRequestStatus)999, null, null));
            Assert.Throws<InvalidOperationException>(
                () => OnboardingStatusMapper.Map(OnboardingRequestStatus.AwaitingPayment, (TenantLifecycleStatus)999, null));
            Assert.Throws<InvalidOperationException>(
                () => OnboardingStatusMapper.Map(OnboardingRequestStatus.AwaitingPayment, null, (ProvisioningJobStatus)999));
        }
    }
}
