namespace CafeHub.Domain.Plans
{
    public sealed class PlanEntitlement
    {
        private PlanEntitlement()
        {
        }

        public Guid Id { get; private set; }

        public Guid PlanVersionId { get; private set; }

        public string Key { get; private set; } = string.Empty;

        public string Value { get; private set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; private set; }

        public static PlanEntitlement Create(
            Guid id,
            Guid planVersionId,
            string key,
            string value,
            DateTimeOffset createdAt)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The plan entitlement id is required.", nameof(id));
            }

            if (planVersionId == Guid.Empty)
            {
                throw new ArgumentException("The plan version id is required.", nameof(planVersionId));
            }

            if (!IsWellFormedKey(key))
            {
                throw new ArgumentException(
                    "The entitlement key is required and must start with a lowercase letter and contain only lowercase letters, digits, underscores, and dots, up to 128 characters.",
                    nameof(key));
            }

            if (string.IsNullOrEmpty(value) || value.Length > 512)
            {
                throw new ArgumentException("The entitlement value must be between 1 and 512 characters.", nameof(value));
            }

            return new PlanEntitlement
            {
                Id = id,
                PlanVersionId = planVersionId,
                Key = key,
                Value = value,
                CreatedAt = createdAt,
            };
        }

        private static bool IsWellFormedKey(string? key)
        {
            if (key is null || key.Length is < 1 or > 128)
            {
                return false;
            }

            if (!char.IsBetween(key[0], 'a', 'z'))
            {
                return false;
            }

            return key.All(character =>
                char.IsBetween(character, 'a', 'z')
                || char.IsBetween(character, '0', '9')
                || character is '_' or '.');
        }
    }
}
