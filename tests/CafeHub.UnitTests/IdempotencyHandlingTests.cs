using CafeHub.Application.Abstractions.Behaviors;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Idempotency;
using CafeHub.WebApi.Idempotency;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace CafeHub.UnitTests
{
    public sealed class IdempotencyHandlingTests
    {
        private sealed class TestIdempotentCommand : IIdempotentCommand
        {
            public string? IdempotencyKey { get; set; }
        }

        private sealed class TestIdempotentCommandValidator : AbstractValidator<TestIdempotentCommand>
        {
            public TestIdempotentCommandValidator()
            {
                RuleFor(command => command.IdempotencyKey).MustHaveValidIdempotencyKey();
            }
        }

        private static async Task<Result> RunThroughValidationBehavior(TestIdempotentCommand command)
        {
            var behavior = new ValidationBehavior<TestIdempotentCommand, Result>([new TestIdempotentCommandValidator()]);
            RequestHandlerDelegate<Result> next = (_) => Task.FromResult(Result.Success());

            return await behavior.Handle(command, next, CancellationToken.None);
        }

        [Fact]
        public async Task ACommandWithoutAKey_FailsValidation_ThroughTheValidationBehavior()
        {
            var result = await RunThroughValidationBehavior(new TestIdempotentCommand());

            Assert.True(result.IsFailure);
            var validationError = Assert.IsType<ValidationError>(result.Error);
            var fieldError = Assert.Single(validationError.Errors);
            Assert.Equal("IdempotencyKey", fieldError.Field);
            Assert.Contains("Idempotency-Key", fieldError.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ACommandWithAnInvalidKey_FailsValidation_WithoutEchoingTheKey()
        {
            const string invalidKey = "invalid key +/=";

            var result = await RunThroughValidationBehavior(new TestIdempotentCommand { IdempotencyKey = invalidKey });

            Assert.True(result.IsFailure);
            var validationError = Assert.IsType<ValidationError>(result.Error);
            Assert.Single(validationError.Errors);
            Assert.DoesNotContain(invalidKey, validationError.Errors.Single().Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ACommandWithAValidKey_PassesValidation_AndCallsNext()
        {
            var result = await RunThroughValidationBehavior(new TestIdempotentCommand { IdempotencyKey = "abcd1234" });

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task TheFilter_FillsTheCommandKey_FromTheHeader()
        {
            var command = new TestIdempotentCommand();
            var context = CreateFilterContext(command);
            context.HttpContext.Request.Headers["Idempotency-Key"] = "abcd1234";
            var filter = new IdempotencyKeyActionFilter();

            await filter.OnActionExecutionAsync(context, CreateNext);

            Assert.Equal("abcd1234", command.IdempotencyKey);
        }

        [Fact]
        public async Task TheFilter_LeavesTheCommandKeyEmpty_WhenTheHeaderIsMissing()
        {
            var command = new TestIdempotentCommand();
            var context = CreateFilterContext(command);
            var filter = new IdempotencyKeyActionFilter();

            await filter.OnActionExecutionAsync(context, CreateNext);

            Assert.Null(command.IdempotencyKey);
        }

        private static ActionExecutingContext CreateFilterContext(TestIdempotentCommand command)
        {
            var httpContext = new DefaultHttpContext();
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

            return new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object?> { ["command"] = command },
                controller: new object());
        }

        private static Task<ActionExecutedContext> CreateNext()
        {
            return Task.FromResult<ActionExecutedContext>(new ActionExecutedContext(
                new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
                new List<IFilterMetadata>(),
                controller: new object()));
        }
    }
}
