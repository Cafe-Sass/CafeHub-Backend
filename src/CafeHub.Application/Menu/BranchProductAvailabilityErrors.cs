using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Menu
{
    public static class BranchProductAvailabilityErrors
    {
        public static Error ProductNotActive() =>
            ValidationError.Create([new("productId", "The product must be active.")]);

        public static Error BranchNotActive() =>
            ValidationError.Create([new("branchId", "The branch must be active.")]);
    }
}
