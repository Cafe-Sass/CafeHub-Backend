using System.Globalization;
using CafeHub.Application.Audit;
using CafeHub.Domain.Tenants;

namespace CafeHub.Infrastructure.Audit
{
    internal static class TenantAuditChangeSets
    {
        public static TenantAuditChanges CafeProfileUpdate(
            CafeSettings settings,
            string cafeName,
            string? primaryColor,
            string? secondaryColor,
            string? accentColor,
            string timezone)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(CafeSettings.CafeName), settings.CafeName, cafeName),
                new TenantAuditChanges.FieldChange(nameof(CafeSettings.PrimaryColor), settings.PrimaryColor, primaryColor),
                new TenantAuditChanges.FieldChange(nameof(CafeSettings.SecondaryColor), settings.SecondaryColor, secondaryColor),
                new TenantAuditChanges.FieldChange(nameof(CafeSettings.AccentColor), settings.AccentColor, accentColor),
                new TenantAuditChanges.FieldChange(nameof(CafeSettings.Timezone), settings.Timezone, timezone),
            });
        }

        public static TenantAuditChanges BranchCreate(Branch branch)
        {
            ArgumentNullException.ThrowIfNull(branch);
            return TenantAuditChanges.Create(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(Branch.Name), null, branch.Name),
                new TenantAuditChanges.FieldChange(nameof(Branch.OpeningHoursSummary), null, branch.OpeningHoursSummary),
                new TenantAuditChanges.FieldChange(nameof(Branch.LifecycleStatus), null, branch.LifecycleStatus.ToString()),
            });
        }

        public static TenantAuditChanges BranchUpdate(
            Branch branch,
            string name,
            string? openingHoursSummary)
        {
            ArgumentNullException.ThrowIfNull(branch);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(Branch.Name), branch.Name, name),
                new TenantAuditChanges.FieldChange(nameof(Branch.OpeningHoursSummary), branch.OpeningHoursSummary, openingHoursSummary),
            });
        }

        public static TenantAuditChanges BranchLifecycleStatus(Branch branch, BranchLifecycleStatus newStatus)
        {
            ArgumentNullException.ThrowIfNull(branch);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(
                    nameof(Branch.LifecycleStatus),
                    branch.LifecycleStatus.ToString(),
                    newStatus.ToString()),
            });
        }

        public static TenantAuditChanges StaffCreate(TenantUser user, Branch branch)
        {
            ArgumentNullException.ThrowIfNull(user);
            ArgumentNullException.ThrowIfNull(branch);
            return TenantAuditChanges.Create(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(TenantUser.FullName), null, user.FullName),
                new TenantAuditChanges.FieldChange(nameof(TenantUser.BranchId), null, branch.Id.ToString()),
                new TenantAuditChanges.FieldChange("IsActive", null, bool.TrueString),
            });
        }

        public static TenantAuditChanges StaffUpdate(
            TenantUser user,
            string fullName,
            Guid newBranchId)
        {
            ArgumentNullException.ThrowIfNull(user);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(TenantUser.FullName), user.FullName, fullName),
                new TenantAuditChanges.FieldChange(nameof(TenantUser.BranchId), user.BranchId?.ToString(), newBranchId.ToString()),
            });
        }

        public static TenantAuditChanges StaffLifecycleStatus(TenantUser user, bool newIsActive)
        {
            ArgumentNullException.ThrowIfNull(user);
            var currentIsActive = user.DeletedAt is null;
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange("IsActive", currentIsActive.ToString(), newIsActive.ToString()),
            });
        }

        public static TenantAuditChanges StationCreate(Station station)
        {
            ArgumentNullException.ThrowIfNull(station);
            return TenantAuditChanges.Create(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(Station.Name), null, station.Name),
                new TenantAuditChanges.FieldChange(
                    nameof(Station.SortOrder),
                    null,
                    station.SortOrder.ToString(CultureInfo.InvariantCulture)),
                new TenantAuditChanges.FieldChange(nameof(Station.Status), null, station.Status.ToString()),
                new TenantAuditChanges.FieldChange(nameof(Station.BranchId), null, station.BranchId.ToString()),
            });
        }

        public static TenantAuditChanges StationUpdate(Station station, string name, int sortOrder)
        {
            ArgumentNullException.ThrowIfNull(station);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(Station.Name), station.Name, name),
                new TenantAuditChanges.FieldChange(
                    nameof(Station.SortOrder),
                    station.SortOrder.ToString(CultureInfo.InvariantCulture),
                    sortOrder.ToString(CultureInfo.InvariantCulture)),
            });
        }

        public static TenantAuditChanges StationLifecycleStatus(Station station, StationStatus newStatus)
        {
            ArgumentNullException.ThrowIfNull(station);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(
                    nameof(Station.Status),
                    station.Status.ToString(),
                    newStatus.ToString()),
            });
        }

        public static TenantAuditChanges TableCreate(BranchTable table)
        {
            ArgumentNullException.ThrowIfNull(table);
            return TenantAuditChanges.Create(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(BranchTable.Number), null, table.Number.ToString(CultureInfo.InvariantCulture)),
                new TenantAuditChanges.FieldChange(nameof(BranchTable.Label), null, table.Label),
                new TenantAuditChanges.FieldChange(nameof(BranchTable.Status), null, table.Status.ToString()),
                new TenantAuditChanges.FieldChange(nameof(BranchTable.BranchId), null, table.BranchId.ToString()),
            });
        }

        public static TenantAuditChanges TableUpdate(BranchTable table, string? label)
        {
            ArgumentNullException.ThrowIfNull(table);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(BranchTable.Label), table.Label, label),
            });
        }

        public static TenantAuditChanges TableBulkCreate(int count, int fromNumber, int toNumber)
        {
            return TenantAuditChanges.Create(new[]
            {
                new TenantAuditChanges.FieldChange("count", null, count.ToString(CultureInfo.InvariantCulture)),
                new TenantAuditChanges.FieldChange("fromNumber", null, fromNumber.ToString(CultureInfo.InvariantCulture)),
                new TenantAuditChanges.FieldChange("toNumber", null, toNumber.ToString(CultureInfo.InvariantCulture)),
            });
        }

        public static TenantAuditChanges TableBulkDeactivate(int count, int fromNumber, int toNumber)
        {
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange("count", null, count.ToString(CultureInfo.InvariantCulture)),
                new TenantAuditChanges.FieldChange("fromNumber", null, fromNumber.ToString(CultureInfo.InvariantCulture)),
                new TenantAuditChanges.FieldChange("toNumber", null, toNumber.ToString(CultureInfo.InvariantCulture)),
            });
        }

        public static TenantAuditChanges TableLifecycleStatus(BranchTable table, BranchTableStatus newStatus)
        {
            ArgumentNullException.ThrowIfNull(table);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(
                    nameof(BranchTable.Status),
                    table.Status.ToString(),
                    newStatus.ToString()),
            });
        }

        public static TenantAuditChanges StaffStations(TenantUser staff, IReadOnlyList<Guid> stationIds)
        {
            ArgumentNullException.ThrowIfNull(staff);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(
                    "StationIds",
                    null,
                    string.Join(",", stationIds.OrderBy(id => id).Select(id => id.ToString()))),
            });
        }

        public static TenantAuditChanges ProductCreate(Product product) =>
            TenantAuditChanges.Create(ProductFields(product)
                .Select(field => new TenantAuditChanges.FieldChange(field.Key, null, field.Value)));

        public static TenantAuditChanges ProductUpdate(IReadOnlyDictionary<string, string?> before, Product product)
        {
            var after = ProductFields(product);
            return TenantAuditChanges.Update(before.Keys.Union(after.Keys, StringComparer.Ordinal)
                .Select(key =>
                {
                    var beforePresent = before.TryGetValue(key, out var beforeValue);
                    var afterPresent = after.TryGetValue(key, out var afterValue);
                    return new TenantAuditChanges.FieldChange(key, beforeValue, afterValue, beforePresent, afterPresent);
                })
                .Where(field => field.HasChanged));
        }

        public static IReadOnlyDictionary<string, string?> ProductFields(Product product)
        {
            ArgumentNullException.ThrowIfNull(product);
            var fields = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [nameof(Product.Name)] = product.Name,
                [nameof(Product.Description)] = product.Description,
                [nameof(Product.Ingredients)] = product.Ingredients,
                [nameof(Product.Kind)] = product.Kind.ToString(),
                [nameof(Product.Caffeine)] = product.Caffeine.ToString(),
                [nameof(Product.PreparationMinutes)] = FormatNumber(product.PreparationMinutes),
                [nameof(Product.SortOrder)] = product.SortOrder.ToString(CultureInfo.InvariantCulture),
                [nameof(Product.Status)] = product.Status.ToString(),
                [nameof(Product.CategoryId)] = product.CategoryId.ToString(),
                [nameof(Product.StationId)] = product.StationId?.ToString(),
            };

            foreach (var size in product.Sizes)
            {
                fields[$"price.size.{size.Id:D}"] = FormatNumber(size.Price);
                fields[$"size.{size.Id:D}.name"] = size.Name;
                fields[$"size.{size.Id:D}.status"] = size.Status.ToString();
                fields[$"size.{size.Id:D}.default"] = size.IsDefault.ToString();
            }

            foreach (var addon in product.Addons)
            {
                fields[$"price.addon.{addon.Id:D}"] = FormatNumber(addon.Price);
                fields[$"addon.{addon.Id:D}.name"] = addon.Name;
                fields[$"addon.{addon.Id:D}.status"] = addon.Status.ToString();
            }

            foreach (var tag in product.Tags)
            {
                fields[$"tag.{tag.Id:D}.label"] = tag.Tag;
                fields[$"tag.{tag.Id:D}.status"] = tag.Status.ToString();
            }

            return fields;
        }


        public static TenantAuditChanges BranchProductAvailabilityCreate(BranchProductAvailability row)
        {
            ArgumentNullException.ThrowIfNull(row);
            return TenantAuditChanges.Create(new[]
            {
                new TenantAuditChanges.FieldChange(
                    nameof(BranchProductAvailability.BranchId), null, row.BranchId.ToString()),
                new TenantAuditChanges.FieldChange(
                    nameof(BranchProductAvailability.IsAvailable), null, row.IsAvailable.ToString()),
                new TenantAuditChanges.FieldChange(
                    nameof(BranchProductAvailability.ReasonCode), null, row.ReasonCode),
            });
        }

        public static TenantAuditChanges BranchProductAvailabilityUpdate(
            bool previousAvailable, string previousReason, BranchProductAvailability row)
        {
            ArgumentNullException.ThrowIfNull(row);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(
                    nameof(BranchProductAvailability.IsAvailable),
                    previousAvailable.ToString(),
                    row.IsAvailable.ToString()),
                new TenantAuditChanges.FieldChange(
                    nameof(BranchProductAvailability.ReasonCode), previousReason, row.ReasonCode),
            });
        }

        public static TenantAuditChanges ProductLifecycleStatus(Product product, ProductStatus oldStatus)
        {
            ArgumentNullException.ThrowIfNull(product);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(
                    nameof(Product.Status),
                    oldStatus.ToString(),
                    product.Status.ToString()),
            });
        }

        public static TenantAuditChanges GuestMuteCreate(GuestMute mute)
        {
            ArgumentNullException.ThrowIfNull(mute);
            return TenantAuditChanges.Create(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(GuestMute.Reason), null, mute.Reason),
                new TenantAuditChanges.FieldChange(
                    nameof(GuestMute.ExpiresAt), null, mute.ExpiresAt.ToString("O", CultureInfo.InvariantCulture)),
                new TenantAuditChanges.FieldChange(nameof(GuestMute.BranchId), null, mute.BranchId.ToString()),
            });
        }

        public static TenantAuditChanges GuestMuteLift(GuestMute mute)
        {
            ArgumentNullException.ThrowIfNull(mute);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(GuestMute.Reason), mute.Reason, mute.Reason),
                new TenantAuditChanges.FieldChange(
                    nameof(GuestMute.ExpiresAt),
                    mute.ExpiresAt.ToString("O", CultureInfo.InvariantCulture),
                    mute.ExpiresAt.ToString("O", CultureInfo.InvariantCulture)),
                new TenantAuditChanges.FieldChange(nameof(GuestMute.BranchId), mute.BranchId.ToString(), mute.BranchId.ToString()),
                new TenantAuditChanges.FieldChange(
                    nameof(GuestMute.LiftedAt), null, mute.LiftedAt?.ToString("O", CultureInfo.InvariantCulture)),
            });
        }

        public static TenantAuditChanges PaymentConfirmCash(Payment payment)
        {
            ArgumentNullException.ThrowIfNull(payment);
            return TenantAuditChanges.Create(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(Payment.Method), null, payment.Method.ToString()),
                new TenantAuditChanges.FieldChange(nameof(Payment.Status), null, payment.Status.ToString()),
                new TenantAuditChanges.FieldChange(nameof(Payment.Amount), null, FormatNumber(payment.Amount)),
                new TenantAuditChanges.FieldChange(nameof(Payment.BillId), null, payment.BillId.ToString()),
                new TenantAuditChanges.FieldChange(nameof(Payment.OrderId), null, payment.OrderId?.ToString()),
            });
        }

        public static TenantAuditChanges WifiSettingsCreate(WifiSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return TenantAuditChanges.Create(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(WifiSettings.Policy), null, settings.Policy.ToString()),
            });
        }

        public static TenantAuditChanges WifiSettingsPolicy(WifiPolicy previousPolicy, WifiPolicy policy)
        {
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(
                    nameof(WifiSettings.Policy), previousPolicy.ToString(), policy.ToString()),
            });
        }

        public static TenantAuditChanges BranchWifiNetworkCreate(
            Guid branchId,
            string ssid,
            WifiSecurity security,
            string? password)
        {
            return TenantAuditChanges.Create(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(BranchWifiNetwork.BranchId), null, branchId.ToString()),
                new TenantAuditChanges.FieldChange(nameof(BranchWifiNetwork.Ssid), null, ssid),
                new TenantAuditChanges.FieldChange(nameof(BranchWifiNetwork.Security), null, security.ToString()),
                new TenantAuditChanges.FieldChange(nameof(BranchWifiNetwork.Password), null, password),
            });
        }

        public static TenantAuditChanges BranchWifiNetworkUpdate(
            BranchWifiNetwork network,
            string ssid,
            WifiSecurity security,
            string? password)
        {
            ArgumentNullException.ThrowIfNull(network);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(BranchWifiNetwork.Ssid), network.Ssid, ssid),
                new TenantAuditChanges.FieldChange(
                    nameof(BranchWifiNetwork.Security), network.Security.ToString(), security.ToString()),
                new TenantAuditChanges.FieldChange(nameof(BranchWifiNetwork.Password), network.Password, password),
            });
        }

        public static TenantAuditChanges BranchWifiNetworkClear(BranchWifiNetwork network)
        {
            ArgumentNullException.ThrowIfNull(network);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(BranchWifiNetwork.Ssid), network.Ssid, null),
                new TenantAuditChanges.FieldChange(
                    nameof(BranchWifiNetwork.Security), network.Security.ToString(), null),
                new TenantAuditChanges.FieldChange(nameof(BranchWifiNetwork.Password), network.Password, null),
            });
        }

        public static TenantAuditChanges WifiPackageCreate(WifiPackage package)
        {
            ArgumentNullException.ThrowIfNull(package);
            return TenantAuditChanges.Create(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(WifiPackage.Name), null, package.Name),
                new TenantAuditChanges.FieldChange(
                    nameof(WifiPackage.DurationMinutes), null, package.DurationMinutes.ToString(CultureInfo.InvariantCulture)),
                new TenantAuditChanges.FieldChange(
                    nameof(WifiPackage.SpeedMbps), null, package.SpeedMbps?.ToString(CultureInfo.InvariantCulture)),
                new TenantAuditChanges.FieldChange(
                    nameof(WifiPackage.DataMode), null, package.DataMode.ToString()),
                new TenantAuditChanges.FieldChange(
                    nameof(WifiPackage.QuotaMb), null, package.QuotaMb?.ToString(CultureInfo.InvariantCulture)),
                new TenantAuditChanges.FieldChange(
                    nameof(WifiPackage.Price), null, package.Price.ToString(CultureInfo.InvariantCulture)),
                new TenantAuditChanges.FieldChange(
                    nameof(WifiPackage.SortOrder), null, package.SortOrder.ToString(CultureInfo.InvariantCulture)),
                new TenantAuditChanges.FieldChange(
                    nameof(WifiPackage.Status), null, package.Status.ToString()),
            });
        }

        public static TenantAuditChanges WifiPackageUpdate(
            WifiPackageAuditValues before,
            WifiPackage package)
        {
            ArgumentNullException.ThrowIfNull(before);
            ArgumentNullException.ThrowIfNull(package);
            return TenantAuditChanges.Update(new[]
                {
                    new TenantAuditChanges.FieldChange(nameof(WifiPackage.Name), before.Name, package.Name),
                    new TenantAuditChanges.FieldChange(
                        nameof(WifiPackage.DurationMinutes), before.DurationMinutesText,
                        package.DurationMinutes.ToString(CultureInfo.InvariantCulture)),
                    new TenantAuditChanges.FieldChange(
                        nameof(WifiPackage.SpeedMbps), before.SpeedMbpsText,
                        package.SpeedMbps?.ToString(CultureInfo.InvariantCulture)),
                    new TenantAuditChanges.FieldChange(
                        nameof(WifiPackage.DataMode), before.DataMode.ToString(), package.DataMode.ToString()),
                    new TenantAuditChanges.FieldChange(
                        nameof(WifiPackage.QuotaMb), before.QuotaMbText,
                        package.QuotaMb?.ToString(CultureInfo.InvariantCulture)),
                    new TenantAuditChanges.FieldChange(
                        nameof(WifiPackage.Price), before.PriceText,
                        package.Price.ToString(CultureInfo.InvariantCulture)),
                    new TenantAuditChanges.FieldChange(
                        nameof(WifiPackage.SortOrder), before.SortOrderText,
                        package.SortOrder.ToString(CultureInfo.InvariantCulture)),
                }
                .Where(field => field.HasChanged));
        }

        public static TenantAuditChanges PaymentReview(Payment payment, string? rejectionReason)
        {
            ArgumentNullException.ThrowIfNull(payment);
            var fields = new List<TenantAuditChanges.FieldChange>
            {
                new(nameof(Payment.Status), PaymentStatus.PendingReview.ToString(), payment.Status.ToString()),
                new(
                    nameof(Payment.Amount),
                    payment.ClaimedAmount is { } claimed ? FormatNumber(claimed) : null,
                    FormatNumber(payment.Amount)),
                new(
                    nameof(Payment.ConfirmedAt),
                    null,
                    payment.ConfirmedAt?.ToString("O", CultureInfo.InvariantCulture)),
            };
            if (rejectionReason is not null)
            {
                fields.Add(new TenantAuditChanges.FieldChange("RejectionReason", null, rejectionReason));
            }

            return TenantAuditChanges.Update(fields.Where(field => field.HasChanged));
        }

        public static TenantAuditChanges BillOpen(Bill bill)
        {
            ArgumentNullException.ThrowIfNull(bill);
            return TenantAuditChanges.Create(new[]
            {
                new TenantAuditChanges.FieldChange(nameof(Bill.Status), null, bill.Status.ToString()),
                new TenantAuditChanges.FieldChange(nameof(Bill.PublicReference), null, bill.PublicReference),
                new TenantAuditChanges.FieldChange(nameof(Bill.BranchId), null, bill.BranchId.ToString()),
            });
        }

        public static TenantAuditChanges PaymentSettingsUpdate(
            CafePaymentSettings settings,
            bool onlineEnabled,
            string methodLabel,
            string? paymentLink,
            string? paymentAddress,
            string? displayName)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(
                    nameof(CafePaymentSettings.OnlineEnabled),
                    settings.OnlineEnabled.ToString(),
                    onlineEnabled.ToString()),
                new TenantAuditChanges.FieldChange(nameof(CafePaymentSettings.MethodLabel), settings.MethodLabel, methodLabel),
                new TenantAuditChanges.FieldChange(nameof(CafePaymentSettings.PaymentLink), settings.PaymentLink, paymentLink),
                new TenantAuditChanges.FieldChange(nameof(CafePaymentSettings.PaymentAddress), settings.PaymentAddress, paymentAddress),
                new TenantAuditChanges.FieldChange(nameof(CafePaymentSettings.DisplayName), settings.DisplayName, displayName),
            });
        }

        public static TenantAuditChanges BillClose(Bill bill, decimal remainingAmount)
        {
            ArgumentNullException.ThrowIfNull(bill);
            return TenantAuditChanges.Update(new[]
            {
                new TenantAuditChanges.FieldChange(
                    nameof(Bill.Status),
                    BillStatus.Open.ToString(),
                    bill.Status.ToString()),
                new TenantAuditChanges.FieldChange(nameof(Bill.CloseReason), null, bill.CloseReason),
                new TenantAuditChanges.FieldChange("RemainingAmount", null, FormatNumber(remainingAmount)),
            });
        }

        private static string? FormatNumber(int? value) => value?.ToString(CultureInfo.InvariantCulture);

        private static string FormatNumber(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
