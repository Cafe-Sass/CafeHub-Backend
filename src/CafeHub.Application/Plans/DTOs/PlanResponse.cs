using CafeHub.Domain.Plans;

namespace CafeHub.Application.Plans.DTOs
{
    public sealed class PlanResponse
    {
        public required Guid Id { get; init; }

        public required string Key { get; init; }

        public required string Name { get; init; }

        public required bool IsSelectable { get; init; }

        public required IReadOnlyList<PlanVersionResponse> Versions { get; init; }

        public required DateTimeOffset CreatedAt { get; init; }

        public required DateTimeOffset UpdatedAt { get; init; }

        public static PlanResponse From(Plan plan)
        {
            ArgumentNullException.ThrowIfNull(plan);

            return new PlanResponse
            {
                Id = plan.Id,
                Key = plan.Code,
                Name = plan.DisplayName,
                IsSelectable = plan.IsSelectable,
                Versions = plan.Versions
                    .OrderByDescending(version => version.VersionNumber)
                    .Select(PlanVersionResponse.From)
                    .ToArray(),
                CreatedAt = plan.CreatedAt,
                UpdatedAt = plan.UpdatedAt,
            };
        }
    }
}
