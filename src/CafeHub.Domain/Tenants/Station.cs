namespace CafeHub.Domain.Tenants
{
    public sealed class Station
    {
        public const int MaximumNameLength = 64;

        private Station()
        {
        }

        public Guid Id { get; private set; }

        public Guid BranchId { get; private set; }

        public string Name { get; private set; } = string.Empty;

        public StationStatus Status { get; private set; }

        public int SortOrder { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static Station Create(
            Guid id,
            Guid branchId,
            string name,
            int sortOrder,
            DateTimeOffset createdAt,
            Guid? createdBy = null)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The station id is required.", nameof(id));
            }

            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The branch id is required.", nameof(branchId));
            }

            return new Station
            {
                Id = id,
                BranchId = branchId,
                Name = ValidateName(name),
                Status = StationStatus.Active,
                SortOrder = ValidateSortOrder(sortOrder),
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                CreatedBy = createdBy,
                UpdatedBy = createdBy,
            };
        }

        public void Rename(string name, int sortOrder, DateTimeOffset updatedAt, Guid? updatedBy)
        {
            Name = ValidateName(name);
            SortOrder = ValidateSortOrder(sortOrder);
            UpdatedAt = updatedAt;
            UpdatedBy = updatedBy;
        }

        public void Deactivate(DateTimeOffset updatedAt, Guid? updatedBy)
        {
            Status = StationStatus.Inactive;
            UpdatedAt = updatedAt;
            UpdatedBy = updatedBy;
        }

        public void Reactivate(DateTimeOffset updatedAt, Guid? updatedBy)
        {
            Status = StationStatus.Active;
            UpdatedAt = updatedAt;
            UpdatedBy = updatedBy;
        }

        private static string ValidateName(string name)
        {
            var trimmed = name?.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaximumNameLength)
            {
                throw new ArgumentException(
                    "The station name must be between 1 and 64 characters after trimming.",
                    nameof(name));
            }

            if (trimmed.Any(char.IsControl))
            {
                throw new ArgumentException("The station name must not contain control characters.", nameof(name));
            }

            return trimmed;
        }

        private static int ValidateSortOrder(int sortOrder)
        {
            if (sortOrder < 0)
            {
                throw new ArgumentException("The station sort order must not be negative.", nameof(sortOrder));
            }

            return sortOrder;
        }
    }
}
