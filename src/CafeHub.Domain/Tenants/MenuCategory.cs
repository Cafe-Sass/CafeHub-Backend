using System.Buffers;
using System.Text;

namespace CafeHub.Domain.Tenants
{
    public sealed class MenuCategory
    {
        public const int MaximumNameLength = 64;

        private MenuCategory()
        {
        }

        public Guid Id { get; private set; }
        public Guid? ParentId { get; private set; }
        public MenuCategory? Parent { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public int SortOrder { get; private set; }
        public MenuCategoryStatus Status { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset UpdatedAt { get; private set; }
        public Guid? CreatedBy { get; private set; }
        public Guid? UpdatedBy { get; private set; }

        public static MenuCategory Create(
            Guid id, MenuCategory? parent, string name, int sortOrder, DateTimeOffset now, Guid actorId)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("A category id is required.", nameof(id));
            }

            if (parent is not null && (parent.Id == id || !parent.IsActiveRoot()))
            {
                throw new ArgumentException("The parent must be an active root category.", nameof(parent));
            }

            return new MenuCategory
            {
                Id = id,
                ParentId = parent?.Id,
                Name = NormalizeName(name),
                SortOrder = ValidateOrder(sortOrder),
                Status = MenuCategoryStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            };
        }

        public bool IsActiveRoot() => ParentId is null && Status == MenuCategoryStatus.Active;

        public bool Update(string name, int sortOrder, DateTimeOffset now, Guid actorId)
        {
            var trimmed = NormalizeName(name);
            ValidateOrder(sortOrder);
            if (Name == trimmed && SortOrder == sortOrder)
            {
                return false;
            }

            Name = trimmed;
            SortOrder = sortOrder;
            UpdatedAt = now;
            UpdatedBy = actorId;
            return true;
        }

        public bool ChangeStatus(MenuCategoryStatus status, MenuCategory? parent, DateTimeOffset now, Guid actorId)
        {
            if (!Enum.IsDefined(status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            if (status == Status)
            {
                return false;
            }

            if (status == MenuCategoryStatus.Active && ParentId is { } parentId
                && (parent is null || parent.Id != parentId || !parent.IsActiveRoot()))
            {
                throw new InvalidOperationException("A child can only be reactivated under an active root.");
            }

            Status = status;
            UpdatedAt = now;
            UpdatedBy = actorId;
            return true;
        }

        public static bool IsValidName(string? name)
        {
            var remaining = name.AsSpan().Trim();
            if (remaining.IsEmpty)
            {
                return false;
            }

            var count = 0;
            while (!remaining.IsEmpty)
            {
                if (Rune.DecodeFromUtf16(remaining, out var scalar, out var consumed) != OperationStatus.Done
                    || scalar.Value == 0 || ++count > MaximumNameLength)
                {
                    return false;
                }

                remaining = remaining[consumed..];
            }

            return true;
        }

        private static string NormalizeName(string name)
        {
            if (!IsValidName(name))
            {
                throw new ArgumentException("The name must contain 1 to 64 Unicode scalars after trimming, with no NUL.", nameof(name));
            }

            return name.Trim();
        }

        private static int ValidateOrder(int sortOrder)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sortOrder);
            return sortOrder;
        }
    }
}
