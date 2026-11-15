using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Staff
{
    public static class StaffErrors
    {
        public const string NotFoundCode = "Staff.NotFound";
        public const string NotFoundMessage = "The staff member was not found.";

        public static Error NotFound()
        {
            return Error.NotFound(NotFoundCode, NotFoundMessage);
        }

        public const string EmailConflictCode = "Staff.EmailConflict";
        public const string EmailConflictMessage = "A user with this email already exists in this cafe.";

        public static Error EmailConflict()
        {
            return Error.Conflict(EmailConflictCode, EmailConflictMessage);
        }

        public const string BranchNotFoundCode = "Staff.BranchNotFound";
        public const string BranchNotFoundMessage = "The branch was not found.";

        public static Error BranchNotFound()
        {
            return Error.NotFound(BranchNotFoundCode, BranchNotFoundMessage);
        }

        public const string BranchNotActiveCode = "Staff.BranchNotActive";
        public const string BranchNotActiveMessage = "The selected branch is not active.";

        public static ValidationError BranchNotActive()
        {
            return ValidationError.Create(new[] { new FieldError("branchId", BranchNotActiveMessage) });
        }

        public const string CannotModifyOwnerCode = "Staff.CannotModifyOwner";
        public const string CannotModifyOwnerMessage = "The owner account cannot be modified through staff operations.";

        public static Error CannotModifyOwner()
        {
            return Error.Forbidden(CannotModifyOwnerCode, CannotModifyOwnerMessage);
        }

        public const string ConcurrencyConflictCode = "Staff.ConcurrencyConflict";
        public const string ConcurrencyConflictMessage = "The staff member was modified by another request. Please reload and try again.";

        public static Error ConcurrencyConflict()
        {
            return Error.Conflict(ConcurrencyConflictCode, ConcurrencyConflictMessage);
        }

        public const string InvalidConcurrencyTokenCode = "Staff.InvalidConcurrencyToken";
        public const string InvalidConcurrencyTokenMessage = "The concurrency token is not a valid unsigned integer.";

        public static ValidationError InvalidConcurrencyToken()
        {
            return ValidationError.Create(new[] { new FieldError("concurrencyToken", InvalidConcurrencyTokenMessage) });
        }

        public const string InvalidCurrentPasswordCode = "Staff.InvalidCurrentPassword";
        public const string InvalidCurrentPasswordMessage = "The current password is not correct.";

        public static ValidationError InvalidCurrentPassword()
        {
            return ValidationError.Create(new[] { new FieldError("currentPassword", InvalidCurrentPasswordMessage) });
        }
    }
}
