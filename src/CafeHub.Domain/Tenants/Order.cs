namespace CafeHub.Domain.Tenants
{
    public sealed class Order
    {
        public const int MaximumGuestDisplayNameLength = 64;
        public const int MinimumGuestDeviceIdLength = 16;
        public const int MaximumGuestDeviceIdLength = 64;
        public const int MaximumIdempotencyKeyLength = 128;
        public const int MinimumRejectionReasonLength = 3;
        public const int MaximumRejectionReasonLength = 256;
        public const int MaximumItemCount = 50;
        public const int PublicReferenceLength = 22;
        public const int RequestHashLength = 64;

        private readonly List<OrderItem> items = [];

        private Order()
        {
        }

        public Guid Id { get; private set; }

        public Guid BranchId { get; private set; }

        public Guid? TableId { get; private set; }

        public int OrderNumber { get; private set; }

        public DateOnly BusinessDate { get; private set; }

        public OrderServiceType ServiceType { get; private set; }

        public OrderStatus Status { get; private set; }

        public OrderSource Source { get; private set; }

        public string GuestDisplayName { get; private set; } = string.Empty;

        public string? GuestDeviceId { get; private set; }

        public Guid? PlacedByUserId { get; private set; }

        public string PublicReference { get; private set; } = string.Empty;

        public string IdempotencyKey { get; private set; } = string.Empty;

        public string? RequestHash { get; private set; }

        public decimal TotalAmount { get; private set; }

        public DateTimeOffset SubmittedAt { get; private set; }

        public DateTimeOffset ResponseDeadline { get; private set; }

        public Guid? RespondedByUserId { get; private set; }

        public DateTimeOffset? RespondedAt { get; private set; }

        public string? RejectionReason { get; private set; }

        public Guid? BillId { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public IReadOnlyList<OrderItem> Items => items;

        public static Order Create(
            Guid id,
            Guid branchId,
            Guid? tableId,
            int orderNumber,
            DateOnly businessDate,
            OrderServiceType serviceType,
            OrderSource source,
            string guestDisplayName,
            string? guestDeviceId,
            Guid? placedByUserId,
            string publicReference,
            string idempotencyKey,
            string? requestHash,
            DateTimeOffset submittedAt,
            DateTimeOffset responseDeadline,
            IReadOnlyCollection<OrderItem> orderItems,
            Guid? actorId)
        {
            ArgumentNullException.ThrowIfNull(orderItems);
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The order id is required.", nameof(id));
            }

            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The branch id is required.", nameof(branchId));
            }

            if (tableId == Guid.Empty)
            {
                throw new ArgumentException("The table id cannot be empty.", nameof(tableId));
            }

            if (serviceType == OrderServiceType.WifiOnly)
            {
                if (tableId is not null)
                {
                    throw new ArgumentException("A WiFi-only order cannot name a table.", nameof(tableId));
                }
            }
            else if (tableId is null)
            {
                throw new ArgumentException("A table-service order requires a table.", nameof(tableId));
            }

            if (orderNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(orderNumber), "The order number must start at 1.");
            }

            if (!Enum.IsDefined(serviceType))
            {
                throw new ArgumentOutOfRangeException(nameof(serviceType));
            }

            if (!Enum.IsDefined(source))
            {
                throw new ArgumentOutOfRangeException(nameof(source));
            }

            if (placedByUserId == Guid.Empty)
            {
                throw new ArgumentException("The placing user id cannot be empty.", nameof(placedByUserId));
            }

            if (source == OrderSource.Guest && placedByUserId is not null)
            {
                throw new ArgumentException("A guest order cannot name the user that placed it.", nameof(placedByUserId));
            }

            if (orderItems.Count == 0)
            {
                throw new ArgumentException("An order requires at least one item.", nameof(orderItems));
            }

            if (orderItems.Count > MaximumItemCount)
            {
                throw new ArgumentException($"An order accepts at most {MaximumItemCount} items.", nameof(orderItems));
            }

            var seenItemIds = new HashSet<Guid>();
            var seenLineNumbers = new HashSet<int>();
            foreach (var item in orderItems)
            {
                ArgumentNullException.ThrowIfNull(item);
                if (!seenItemIds.Add(item.Id))
                {
                    throw new ArgumentException("Order item ids must be unique within an order.", nameof(orderItems));
                }

                if (!seenLineNumbers.Add(item.LineNumber))
                {
                    throw new ArgumentException("Order line numbers must be unique within an order.", nameof(orderItems));
                }
            }

            if (responseDeadline <= submittedAt)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(responseDeadline), "The response deadline must be after the submission time.");
            }

            var order = new Order
            {
                Id = id,
                BranchId = branchId,
                TableId = tableId,
                OrderNumber = orderNumber,
                BusinessDate = businessDate,
                ServiceType = serviceType,
                Status = OrderStatus.Pending,
                Source = source,
                GuestDisplayName = ValidateGuestDisplayName(guestDisplayName),
                GuestDeviceId = NormalizeGuestDeviceId(guestDeviceId),
                PlacedByUserId = placedByUserId,
                PublicReference = ValidatePublicReference(publicReference),
                IdempotencyKey = ValidateIdempotencyKey(idempotencyKey),
                RequestHash = ValidateRequestHash(requestHash),
                SubmittedAt = submittedAt,
                ResponseDeadline = responseDeadline,
                CreatedAt = submittedAt,
                UpdatedAt = submittedAt,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            };

            foreach (var item in orderItems)
            {
                item.AssignOrder(id);
                order.items.Add(item);
            }

            order.TotalAmount = order.items.Sum(item => item.LineTotal);
            return order;
        }

        public void Accept(Guid responderId, DateTimeOffset now) =>
            ApplyDecision(responderId, OrderStatus.Accepted, null, now);

        public void AcceptAutomatically(DateTimeOffset now)
        {
            if (Status is not OrderStatus.Pending)
            {
                throw new InvalidOperationException(
                    $"Only pending orders can be accepted. The current status is {Status}.");
            }

            Status = OrderStatus.Accepted;
            RespondedByUserId = null;
            RespondedAt = null;
            RejectionReason = null;
            UpdatedAt = now;
            UpdatedBy = null;
        }

        public void Reject(Guid responderId, string reason, DateTimeOffset now) =>
            ApplyDecision(responderId, OrderStatus.Rejected, ValidateRejectionReason(reason), now);

        public void Expire(DateTimeOffset now)
        {
            if (Status is not OrderStatus.Pending)
            {
                throw new InvalidOperationException(
                    $"Only pending orders can expire. The current status is {Status}.");
            }

            if (now < ResponseDeadline)
            {
                throw new InvalidOperationException("The response deadline has not elapsed yet.");
            }

            Status = OrderStatus.Expired;
            UpdatedAt = now;
            UpdatedBy = null;
        }

        public void AttachToBill(Guid billId)
        {
            if (billId == Guid.Empty)
            {
                throw new ArgumentException("The bill id is required.", nameof(billId));
            }

            BillId = billId;
        }

        private void ApplyDecision(Guid responderId, OrderStatus status, string? rejectionReason, DateTimeOffset now)
        {
            if (responderId == Guid.Empty)
            {
                throw new ArgumentException("The responder id is required.", nameof(responderId));
            }

            if (Status is not OrderStatus.Pending)
            {
                throw new InvalidOperationException(
                    $"Only pending orders can be decided. The current status is {Status}.");
            }

            Status = status;
            RespondedByUserId = responderId;
            RespondedAt = now;
            RejectionReason = rejectionReason;
            UpdatedAt = now;
            UpdatedBy = responderId;
        }

        public static bool IsValidRejectionReason(string? reason)
        {
            var trimmed = reason?.Trim();
            return !string.IsNullOrEmpty(trimmed)
                && trimmed.Length >= MinimumRejectionReasonLength
                && trimmed.Length <= MaximumRejectionReasonLength
                && !trimmed.Any(char.IsControl);
        }

        private static string ValidateRejectionReason(string reason)
        {
            if (!IsValidRejectionReason(reason))
            {
                throw new ArgumentException(
                    "The rejection reason must be 3 to 256 characters after trimming, with no control characters.",
                    nameof(reason));
            }

            return reason.Trim();
        }

        public static bool IsValidGuestDisplayName(string? name)
        {
            var trimmed = name?.Trim();
            return !string.IsNullOrEmpty(trimmed)
                && trimmed.Length <= MaximumGuestDisplayNameLength
                && !trimmed.Any(char.IsControl);
        }

        public static bool IsValidGuestDeviceId(string? deviceId)
        {
            var trimmed = deviceId?.Trim();
            return trimmed is not null
                && trimmed.Length >= MinimumGuestDeviceIdLength
                && trimmed.Length <= MaximumGuestDeviceIdLength
                && trimmed.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
        }

        public static bool IsValidPublicReference(string? reference)
        {
            var trimmed = reference?.Trim();
            return trimmed is not null
                && trimmed.Length == PublicReferenceLength
                && trimmed.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
        }

        public static bool IsValidIdempotencyKey(string? key)
        {
            var trimmed = key?.Trim();
            return !string.IsNullOrEmpty(trimmed) && trimmed.Length <= MaximumIdempotencyKeyLength;
        }

        public static bool IsValidRequestHash(string? hash)
        {
            return hash is null
                || (hash.Length == RequestHashLength && hash.All(character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f'));
        }

        private static string ValidateGuestDisplayName(string name)
        {
            if (!IsValidGuestDisplayName(name))
            {
                throw new ArgumentException(
                    "The guest name must be 1 to 64 characters after trimming, with no control characters.",
                    nameof(name));
            }

            return name.Trim();
        }

        private static string? NormalizeGuestDeviceId(string? deviceId)
        {
            if (deviceId is null)
            {
                return null;
            }

            if (!IsValidGuestDeviceId(deviceId))
            {
                throw new ArgumentException(
                    "The guest device id must be 16 to 64 letters, digits, hyphens, or underscores.",
                    nameof(deviceId));
            }

            return deviceId.Trim();
        }

        private static string ValidatePublicReference(string reference)
        {
            if (!IsValidPublicReference(reference))
            {
                throw new ArgumentException(
                    "The public reference must be 22 base64url characters.", nameof(reference));
            }

            return reference;
        }

        private static string ValidateIdempotencyKey(string key)
        {
            if (!IsValidIdempotencyKey(key))
            {
                throw new ArgumentException(
                    "The idempotency key must be 1 to 128 characters.", nameof(key));
            }

            return key.Trim();
        }

        private static string? ValidateRequestHash(string? hash)
        {
            if (!IsValidRequestHash(hash))
            {
                throw new ArgumentException(
                    "The request hash must be 64 lower-case hexadecimal characters.", nameof(hash));
            }

            return hash;
        }
    }
}
