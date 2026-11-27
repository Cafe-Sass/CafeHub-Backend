using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Refunds.Commands
{
    public sealed class ChangeRefundRequestStatusCommandValidator
        : AbstractValidator<ChangeRefundRequestStatusCommand>
    {
        public ChangeRefundRequestStatusCommandValidator()
        {
            RuleFor(command => command.RefundRequestId).NotEmpty();
            RuleFor(command => command.Status)
                .Must(BeAnAllowedTarget)
                .WithMessage("The target status must be InReview, Approved, or Rejected.");
            RuleFor(command => command.Note)
                .Must((command, note) => !RequiresResolutionNote(command.Status) || RefundRequest.IsValidResolutionNote(note))
                .WithMessage("Approved and Rejected require a resolution note.");
        }

        private static bool BeAnAllowedTarget(string? status) =>
            Enum.TryParse<RefundRequestStatus>(status, true, out var parsed)
            && parsed is RefundRequestStatus.InReview
                or RefundRequestStatus.Approved
                or RefundRequestStatus.Rejected;

        private static bool RequiresResolutionNote(string? status) =>
            Enum.TryParse<RefundRequestStatus>(status, true, out var parsed)
            && parsed is RefundRequestStatus.Approved or RefundRequestStatus.Rejected;
    }
}
