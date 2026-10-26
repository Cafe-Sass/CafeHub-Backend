using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Provisioning
{
    public static class ProvisioningErrors
    {
        public const string InvalidWorkerIdCode = "Provisioning.InvalidWorkerId";
        public const string InvalidWorkerIdMessage = "The worker id is required.";

        public const string InvalidLeaseDurationCode = "Provisioning.InvalidLeaseDuration";
        public const string InvalidLeaseDurationMessage = "The lease duration must be positive.";

        public static Error InvalidWorkerId()
        {
            return Error.Validation(InvalidWorkerIdCode, InvalidWorkerIdMessage);
        }

        public static Error InvalidLeaseDuration()
        {
            return Error.Validation(InvalidLeaseDurationCode, InvalidLeaseDurationMessage);
        }
    }
}
