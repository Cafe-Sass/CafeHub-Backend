using System.Globalization;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Billing;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Billing
{
    internal sealed class CafePaymentSettingsStore(
        TenantDbContext context,
        ITenantAuditWriter auditWriter,
        TimeProvider timeProvider) : ICafePaymentSettingsReader, ICafePaymentSettingsWriter
    {
        public async Task<PaymentSettingsResponse?> GetAsync(CancellationToken cancellationToken)
        {
            var settings = await context.CafePaymentSettings.FirstOrDefaultAsync(cancellationToken);
            if (settings is null)
            {
                return null;
            }

            var concurrencyToken = context.Entry(settings)
                .Property<uint>("xmin")
                .CurrentValue
                .ToString(CultureInfo.InvariantCulture);

            return PaymentSettingsResponse.From(settings, concurrencyToken);
        }

        public async Task<GuestPaymentOptionsResponse?> FindOptionsByQrCodeAsync(
            string qrCode,
            CancellationToken cancellationToken)
        {
            var tableExists = await context.BranchTables.AsNoTracking()
                .Join(
                    context.Branches,
                    table => table.BranchId,
                    branch => branch.Id,
                    (table, branch) => new { table, branch })
                .AnyAsync(
                    joined => joined.table.QrCode == qrCode
                        && joined.table.Status == BranchTableStatus.Active
                        && joined.branch.LifecycleStatus == BranchLifecycleStatus.Active,
                    cancellationToken);
            if (!tableExists)
            {
                return null;
            }

            var settings = await context.CafePaymentSettings.AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);
            if (settings is null)
            {
                return null;
            }

            return new GuestPaymentOptionsResponse(
                settings.OnlineEnabled,
                settings.MethodLabel,
                settings.PaymentLink,
                settings.PaymentAddress,
                settings.DisplayName);
        }

        public async Task<Result<PaymentSettingsResponse>> UpdateAsync(
            bool onlineEnabled,
            string? methodLabel,
            string? paymentLink,
            string? paymentAddress,
            string? displayName,
            Guid actorUserId,
            string concurrencyToken,
            CancellationToken cancellationToken)
        {
            var settings = await context.CafePaymentSettings.FirstOrDefaultAsync(cancellationToken);
            if (settings is null)
            {
                return Result.Failure<PaymentSettingsResponse>(PaymentSettingsErrors.NotFound());
            }

            if (!uint.TryParse(concurrencyToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var token))
            {
                return Result.Failure<PaymentSettingsResponse>(
                    ValidationError.Create(new[] { new FieldError("concurrencyToken", "The concurrency token is not a valid unsigned integer.") }));
            }

            var normalizedMethodLabel = string.IsNullOrWhiteSpace(methodLabel)
                ? CafePaymentSettings.DefaultMethodLabel
                : methodLabel.Trim();
            var normalizedLink = string.IsNullOrWhiteSpace(paymentLink) ? null : paymentLink.Trim();
            var normalizedAddress = string.IsNullOrWhiteSpace(paymentAddress) ? null : paymentAddress.Trim();
            var normalizedDisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();

            if (IsNoChange(
                    settings,
                    onlineEnabled,
                    normalizedMethodLabel,
                    normalizedLink,
                    normalizedAddress,
                    normalizedDisplayName))
            {
                var currentToken = context.Entry(settings).Property<uint>("xmin").CurrentValue
                    .ToString(CultureInfo.InvariantCulture);
                return Result.Success(PaymentSettingsResponse.From(settings, currentToken));
            }

            context.Entry(settings).Property<uint>("xmin").OriginalValue = token;
            var changes = TenantAuditChangeSets.PaymentSettingsUpdate(
                settings,
                onlineEnabled,
                normalizedMethodLabel,
                normalizedLink,
                normalizedAddress,
                normalizedDisplayName);

            settings.Update(
                onlineEnabled,
                normalizedMethodLabel,
                normalizedLink,
                normalizedAddress,
                normalizedDisplayName,
                timeProvider.GetUtcNow(),
                actorUserId);

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "payment_settings.update.succeeded",
                TargetType = "cafe_payment_settings",
                TargetId = settings.Id,
                ActorUserId = actorUserId,
                Changes = changes,
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<PaymentSettingsResponse>(auditResult.Error!);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<PaymentSettingsResponse>(PaymentSettingsErrors.ConcurrencyConflict());
            }

            var newToken = context.Entry(settings).Property<uint>("xmin").CurrentValue
                .ToString(CultureInfo.InvariantCulture);
            return Result.Success(PaymentSettingsResponse.From(settings, newToken));
        }

        private static bool IsNoChange(
            CafePaymentSettings settings,
            bool onlineEnabled,
            string methodLabel,
            string? paymentLink,
            string? paymentAddress,
            string? displayName)
        {
            return settings.OnlineEnabled == onlineEnabled
                && settings.MethodLabel == methodLabel
                && settings.PaymentLink == paymentLink
                && settings.PaymentAddress == paymentAddress
                && settings.DisplayName == displayName;
        }
    }
}
