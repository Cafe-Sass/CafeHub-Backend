using CafeHub.Application.Abstractions.Results;

namespace CafeHub.UnitTests
{
    public sealed class ValidationErrorTests
    {
        [Fact]
        public void Create_SetsValidationType_DocumentedCode_AndCarriesFieldErrorsInOrder()
        {
            var fieldErrors = new List<FieldError>
            {
                new("Email", "An email address is required."),
                new("Password", "A password is required."),
                new("BranchName", "A branch name is required."),
            };

            var validationError = ValidationError.Create(fieldErrors);

            Assert.Equal(ErrorType.Validation, validationError.Type);
            Assert.Equal("Validation.Failed", validationError.Code);
            Assert.Equal("One or more validation errors occurred.", validationError.Message);
            Assert.Equal(fieldErrors, validationError.Errors);
        }
    }
}
