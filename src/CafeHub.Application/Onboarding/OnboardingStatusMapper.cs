using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Provisioning;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Onboarding
{
    public static class OnboardingStatusMapper
    {
        public static string Map(
            OnboardingRequestStatus requestStatus,
            TenantLifecycleStatus? tenantStatus,
            ProvisioningJobStatus? jobStatus)
        {
            if (!Enum.IsDefined(requestStatus))
            {
                throw new InvalidOperationException("The onboarding request status is not defined.");
            }

            if (tenantStatus.HasValue && !Enum.IsDefined(tenantStatus.Value))
            {
                throw new InvalidOperationException("The tenant lifecycle status is not defined.");
            }

            if (jobStatus.HasValue && !Enum.IsDefined(jobStatus.Value))
            {
                throw new InvalidOperationException("The provisioning job status is not defined.");
            }

            if (requestStatus != OnboardingRequestStatus.Approved)
            {
                if (tenantStatus.HasValue || jobStatus.HasValue)
                {
                    throw new InvalidOperationException("The onboarding status combination is not mapped.");
                }

                return requestStatus switch
                {
                    OnboardingRequestStatus.AwaitingPayment => OnboardingStatusValues.Registered,
                    OnboardingRequestStatus.PaidAwaitingReview => OnboardingStatusValues.AwaitingReview,
                    OnboardingRequestStatus.Rejected => OnboardingStatusValues.Rejected,
                    OnboardingRequestStatus.PaymentFailed => OnboardingStatusValues.Failed,
                    OnboardingRequestStatus.Cancelled => OnboardingStatusValues.Failed,
                    _ => throw new InvalidOperationException("The onboarding status combination is not mapped."),
                };
            }

            if (tenantStatus is null)
            {
                throw new InvalidOperationException("The onboarding status combination is not mapped.");
            }

            return tenantStatus.Value switch
            {
                TenantLifecycleStatus.Provisioning => jobStatus == ProvisioningJobStatus.Failed
                    ? OnboardingStatusValues.Failed
                    : OnboardingStatusValues.Provisioning,
                TenantLifecycleStatus.Active
                    or TenantLifecycleStatus.Suspended
                    or TenantLifecycleStatus.Terminated => OnboardingStatusValues.Ready,
                _ => throw new InvalidOperationException("The onboarding status combination is not mapped."),
            };
        }
    }
}
