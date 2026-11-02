using System.Globalization;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.CafeProfile;
using CafeHub.Application.CafeProfile.Contracts;
using CafeHub.Application.CafeProfile.DTOs;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.CafeProfile
{
    internal sealed class CafeSettingsWriter(
        TenantDbContext context,
        ITenantAuditWriter auditWriter,
        TimeProvider timeProvider) : ICafeSettingsWriter
    {
        public async Task<Result<CafeProfileResponse>> UpdateAsync(
            string cafeName,
            string? logoMediaReference,
            string? primaryColor,
            string? secondaryColor,
            string? accentColor,
            string? contactPhone,
            string? contactEmail,
            string? contactAddress,
            string timezone,
            Guid actorUserId,
            string concurrencyToken,
            CancellationToken cancellationToken)
        {
            var settings = await context.CafeSettings.FirstOrDefaultAsync(cancellationToken);
            if (settings is null)
            {
                return Result.Failure<CafeProfileResponse>(CafeProfileErrors.NotFound());
            }

            if (!uint.TryParse(concurrencyToken, out var token))
            {
                return Result.Failure<CafeProfileResponse>(
                    ValidationError.Create(new[] { new FieldError("concurrencyToken", "The concurrency token is not a valid unsigned integer.") }));
            }

            if (IsNoChange(settings, cafeName, logoMediaReference, primaryColor, secondaryColor, accentColor, contactPhone, contactEmail, contactAddress, timezone))
            {
                var currentToken = context.Entry(settings).Property<uint>("xmin").CurrentValue.ToString(CultureInfo.InvariantCulture);
                return Result.Success(CafeProfileResponse.From(settings, currentToken));
            }

            context.Entry(settings).Property<uint>("xmin").OriginalValue = token;

            settings.Update(
                cafeName,
                logoMediaReference,
                primaryColor,
                secondaryColor,
                accentColor,
                contactPhone,
                contactEmail,
                contactAddress,
                timezone,
                timeProvider.GetUtcNow(),
                actorUserId);

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "cafe_profile.update.succeeded",
                TargetType = "cafe_settings",
                TargetId = settings.Id,
                ActorUserId = actorUserId,
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<CafeProfileResponse>(auditResult.Error!);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<CafeProfileResponse>(CafeProfileErrors.ConcurrencyConflict());
            }

            var newToken = context.Entry(settings).Property<uint>("xmin").CurrentValue.ToString(CultureInfo.InvariantCulture);
            return Result.Success(CafeProfileResponse.From(settings, newToken));
        }

        private static bool IsNoChange(
            CafeSettings settings,
            string cafeName,
            string? logoMediaReference,
            string? primaryColor,
            string? secondaryColor,
            string? accentColor,
            string? contactPhone,
            string? contactEmail,
            string? contactAddress,
            string timezone)
        {
            return settings.CafeName == cafeName
                && settings.LogoMediaReference == logoMediaReference
                && settings.PrimaryColor == primaryColor
                && settings.SecondaryColor == secondaryColor
                && settings.AccentColor == accentColor
                && settings.ContactPhone == contactPhone
                && settings.ContactEmail == contactEmail
                && settings.ContactAddress == contactAddress
                && settings.Timezone == timezone;
        }
    }
}
