namespace CafeHub.Domain.Tenants
{
    public sealed class Product
    {
        public const int MaximumNameLength = 96;
        public const int MaximumDescriptionLength = 1000;
        public const int MaximumIngredientsLength = 1000;
        public const int MaximumPreparationMinutes = 240;
        public const int MaximumTagCount = 10;

        private readonly List<ProductSize> sizes = [];
        private readonly List<ProductAddon> addons = [];
        private readonly List<ProductTag> tags = [];

        private Product()
        {
        }

        public Guid Id { get; private set; }

        public Guid CategoryId { get; private set; }

        public Guid? StationId { get; private set; }

        public string Name { get; private set; } = string.Empty;

        public string? Description { get; private set; }

        public string? Ingredients { get; private set; }

        public ProductKind Kind { get; private set; }

        public bool Caffeine { get; private set; }

        public int? PreparationMinutes { get; private set; }

        public int SortOrder { get; private set; }

        public ProductStatus Status { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public IReadOnlyList<ProductSize> Sizes => sizes;

        public IReadOnlyList<ProductAddon> Addons => addons;

        public IReadOnlyList<ProductTag> Tags => tags;

        public static Product Create(
            Guid id,
            Guid categoryId,
            Guid? stationId,
            string name,
            string? description,
            string? ingredients,
            ProductKind kind,
            bool caffeine,
            int? preparationMinutes,
            int sortOrder,
            IReadOnlyCollection<ProductSize> sizes,
            IReadOnlyCollection<ProductAddon> addons,
            IEnumerable<string> tags,
            DateTimeOffset now,
            Guid actorId)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The product id is required.", nameof(id));
            }

            if (categoryId == Guid.Empty)
            {
                throw new ArgumentException("The category id is required.", nameof(categoryId));
            }

            if (stationId == Guid.Empty)
            {
                throw new ArgumentException("The station id cannot be empty.", nameof(stationId));
            }

            if (!Enum.IsDefined(kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            ValidatePreparationMinutes(preparationMinutes);
            ArgumentOutOfRangeException.ThrowIfNegative(sortOrder);
            ValidateSizes(sizes);
            ValidateAddons(addons);
            var normalizedTags = NormalizeTags(tags);

            var product = new Product
            {
                Id = id,
                CategoryId = categoryId,
                StationId = stationId,
                Name = ValidateName(name),
                Description = NormalizeOptionalText(description, MaximumDescriptionLength, nameof(description)),
                Ingredients = NormalizeOptionalText(ingredients, MaximumIngredientsLength, nameof(ingredients)),
                Kind = kind,
                Caffeine = caffeine,
                PreparationMinutes = preparationMinutes,
                SortOrder = sortOrder,
                Status = ProductStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            };

            foreach (var size in sizes)
            {
                size.AssignProduct(id);
                product.sizes.Add(size);
            }

            foreach (var addon in addons)
            {
                addon.AssignProduct(id);
                product.addons.Add(addon);
            }

            foreach (var tag in normalizedTags)
            {
                product.tags.Add(ProductTag.Create(id, tag, now));
            }

            return product;
        }

        public bool AddSize(ProductSize size, DateTimeOffset now, Guid actorId)
        {
            ArgumentNullException.ThrowIfNull(size);
            if (size.Status != ProductComponentStatus.Active)
            {
                throw new ArgumentException("Only an active size can be added.", nameof(size));
            }

            if (sizes.Any(existing => existing.Id == size.Id))
            {
                throw new ArgumentException("The size id already belongs to this product.", nameof(size));
            }

            if (sizes.Any(existing => existing.Status == ProductComponentStatus.Active
                && string.Equals(existing.Name, size.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("An active size with this name already exists.", nameof(size));
            }

            if (size.IsDefault && sizes.Any(existing => existing.Status == ProductComponentStatus.Active && existing.IsDefault))
            {
                throw new ArgumentException("The product already has an active default size.", nameof(size));
            }

            size.AssignProduct(Id);
            sizes.Add(size);
            Touch(now, actorId);
            return true;
        }

        public bool DeactivateSize(Guid sizeId, DateTimeOffset now, Guid actorId)
        {
            var size = sizes.SingleOrDefault(existing => existing.Id == sizeId);
            if (size is null)
            {
                throw new ArgumentException("The size does not belong to this product.", nameof(sizeId));
            }

            if (size.Status == ProductComponentStatus.Inactive)
            {
                return false;
            }

            if (size.IsDefault)
            {
                throw new InvalidOperationException("The default size cannot be deactivated without a replacement.");
            }

            size.Deactivate(now, actorId);
            Touch(now, actorId);
            return true;
        }

        public bool AddAddon(ProductAddon addon, DateTimeOffset now, Guid actorId)
        {
            ArgumentNullException.ThrowIfNull(addon);
            if (addon.Status != ProductComponentStatus.Active)
            {
                throw new ArgumentException("Only an active add-on can be added.", nameof(addon));
            }

            if (addons.Any(existing => existing.Id == addon.Id))
            {
                throw new ArgumentException("The add-on id already belongs to this product.", nameof(addon));
            }

            if (addons.Any(existing => existing.Status == ProductComponentStatus.Active
                && string.Equals(existing.Name, addon.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("An active add-on with this name already exists.", nameof(addon));
            }

            addon.AssignProduct(Id);
            addons.Add(addon);
            Touch(now, actorId);
            return true;
        }

        public bool DeactivateAddon(Guid addonId, DateTimeOffset now, Guid actorId)
        {
            var addon = addons.SingleOrDefault(existing => existing.Id == addonId);
            if (addon is null)
            {
                throw new ArgumentException("The add-on does not belong to this product.", nameof(addonId));
            }

            if (addon.Status == ProductComponentStatus.Inactive)
            {
                return false;
            }

            addon.Deactivate(now, actorId);
            Touch(now, actorId);
            return true;
        }

        private static void ValidateSizes(IReadOnlyCollection<ProductSize> productSizes)
        {
            ArgumentNullException.ThrowIfNull(productSizes);
            if (productSizes.Count == 0)
            {
                throw new ArgumentException("A product requires at least one size.", nameof(productSizes));
            }

            var ids = new HashSet<Guid>();
            foreach (var size in productSizes)
            {
                ArgumentNullException.ThrowIfNull(size);
                if (size.Status != ProductComponentStatus.Active)
                {
                    throw new ArgumentException("A new product can contain only active sizes.", nameof(productSizes));
                }

                if (!ids.Add(size.Id))
                {
                    throw new ArgumentException("Size ids must be unique within a product.", nameof(productSizes));
                }
            }

            if (productSizes.Count(size => size.IsDefault) != 1)
            {
                throw new ArgumentException("A product requires exactly one default size.", nameof(productSizes));
            }

            if (productSizes.GroupBy(size => size.Name, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            {
                throw new ArgumentException("Active size names must be unique within a product.", nameof(productSizes));
            }
        }

        private static void ValidateAddons(IReadOnlyCollection<ProductAddon> productAddons)
        {
            ArgumentNullException.ThrowIfNull(productAddons);
            var ids = new HashSet<Guid>();
            foreach (var addon in productAddons)
            {
                ArgumentNullException.ThrowIfNull(addon);
                if (addon.Status != ProductComponentStatus.Active)
                {
                    throw new ArgumentException("A new product can contain only active add-ons.", nameof(productAddons));
                }

                if (!ids.Add(addon.Id))
                {
                    throw new ArgumentException("Add-on ids must be unique within a product.", nameof(productAddons));
                }
            }

            if (productAddons.GroupBy(addon => addon.Name, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            {
                throw new ArgumentException("Active add-on names must be unique within a product.", nameof(productAddons));
            }
        }

        private static List<string> NormalizeTags(IEnumerable<string> productTags)
        {
            ArgumentNullException.ThrowIfNull(productTags);
            var normalized = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tag in productTags)
            {
                var value = ProductTag.Normalize(tag);
                if (!seen.Add(value))
                {
                    throw new ArgumentException("Tags must be unique within a product.", nameof(productTags));
                }

                if (normalized.Count == MaximumTagCount)
                {
                    throw new ArgumentException("A product can have at most 10 tags.", nameof(productTags));
                }

                normalized.Add(value);
            }

            return normalized;
        }

        private static string ValidateName(string name)
        {
            var trimmed = name?.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaximumNameLength)
            {
                throw new ArgumentException(
                    "The product name must be between 1 and 96 characters after trimming.",
                    nameof(name));
            }

            if (trimmed.Any(char.IsControl))
            {
                throw new ArgumentException("The product name must not contain control characters.", nameof(name));
            }

            return trimmed;
        }

        private static string? NormalizeOptionalText(string? value, int maximumLength, string parameterName)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return null;
            }

            if (trimmed.Length > maximumLength)
            {
                throw new ArgumentException(
                    $"The text must not exceed {maximumLength} characters after trimming.",
                    parameterName);
            }

            if (trimmed.Contains('\0'))
            {
                throw new ArgumentException("The text must not contain NUL.", parameterName);
            }

            return trimmed;
        }

        private static void ValidatePreparationMinutes(int? preparationMinutes)
        {
            if (preparationMinutes is < 0 or > MaximumPreparationMinutes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(preparationMinutes),
                    "The preparation minutes must be between 0 and 240.");
            }
        }

        private void Touch(DateTimeOffset now, Guid actorId)
        {
            UpdatedAt = now;
            UpdatedBy = actorId;
        }
    }
}
