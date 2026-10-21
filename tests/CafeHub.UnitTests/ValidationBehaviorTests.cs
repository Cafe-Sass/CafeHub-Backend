using System.Diagnostics;
using CafeHub.Application;
using CafeHub.Application.Abstractions.Behaviors;
using CafeHub.Application.Abstractions.Results;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests
{
    public sealed class ValidationBehaviorTests
    {
        [Fact]
        public async Task Handle_FailingValidator_ReturnsFailedValidationResult_AndSkipsNext()
        {
            var validator = new TestValidator<TestPingCommand>().WithFailure("Name", "A name is required.");
            var behavior = new ValidationBehavior<TestPingCommand, Result>([validator]);
            RequestHandlerDelegate<Result> next = (_) => throw new UnreachableException("Next must not run when validation fails.");

            var result = await behavior.Handle(new TestPingCommand(), next, CancellationToken.None);

            var validationError = Assert.IsType<ValidationError>(result.Error);
            Assert.True(result.IsFailure);
            Assert.Equal(ErrorType.Validation, validationError.Type);
            Assert.Equal(new[] { new FieldError("Name", "A name is required.") }, validationError.Errors);
        }

        [Fact]
        public async Task Handle_TwoFailingValidators_AggregatesAllFailuresInValidatorOrder()
        {
            var firstValidator = new TestValidator<TestPingCommand>()
                .WithFailure("Name", "A name is required.")
                .WithFailure("Email", "An email address is required.");
            var secondValidator = new TestValidator<TestPingCommand>()
                .WithFailure("Phone", "A phone number is required.");
            var behavior = new ValidationBehavior<TestPingCommand, Result>([firstValidator, secondValidator]);
            RequestHandlerDelegate<Result> next = (_) => throw new UnreachableException("Next must not run when validation fails.");

            var result = await behavior.Handle(new TestPingCommand(), next, CancellationToken.None);

            var validationError = Assert.IsType<ValidationError>(result.Error);
            Assert.Equal(ErrorType.Validation, validationError.Type);
            Assert.Equal(new[]
            {
                new FieldError("Name", "A name is required."),
                new FieldError("Email", "An email address is required."),
                new FieldError("Phone", "A phone number is required."),
            }, validationError.Errors);
        }

        [Fact]
        public async Task Handle_NoValidators_CallsNext_AndReturnsItsResult()
        {
            var expectedError = Error.Conflict("Test.NextFailed", "The continuation returned a conflict.");
            var behavior = new ValidationBehavior<TestPingCommand, Result>([]);
            var nextCalled = false;
            RequestHandlerDelegate<Result> next = (_) =>
            {
                nextCalled = true;
                return Task.FromResult(Result.Failure(expectedError));
            };

            var result = await behavior.Handle(new TestPingCommand(), next, CancellationToken.None);

            Assert.True(nextCalled);
            Assert.Same(expectedError, result.Error);
        }

        [Fact]
        public async Task Handle_PassingValidators_CallsNext()
        {
            var validator = new TestValidator<TestPingCommand>();
            var behavior = new ValidationBehavior<TestPingCommand, Result>([validator]);
            var nextCalled = false;
            RequestHandlerDelegate<Result> next = (_) =>
            {
                nextCalled = true;
                return Task.FromResult(Result.Success());
            };

            var result = await behavior.Handle(new TestPingCommand(), next, CancellationToken.None);

            Assert.True(nextCalled);
            Assert.True(result.IsSuccess);
            Assert.Null(result.Error);
        }

        [Fact]
        public async Task Handle_FailingValuedCommand_BuildsFailedResultOfTResponse()
        {
            var validator = new TestValidator<TestPingCommandWithResponse>().WithFailure("Name", "A name is required.");
            var behavior = new ValidationBehavior<TestPingCommandWithResponse, Result<string>>([validator]);
            RequestHandlerDelegate<Result<string>> next = (_) => throw new UnreachableException("Next must not run when validation fails.");

            var result = await behavior.Handle(new TestPingCommandWithResponse("cafe-alpha"), next, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorType.Validation, result.Error!.Type);
        }

        [Fact]
        public async Task Handle_FailingQuery_BuildsFailedResultOfTResponse()
        {
            var validator = new TestValidator<TestBranchCountQuery>().WithFailure("KnownBranches", "One or more branches are required.");
            var behavior = new ValidationBehavior<TestBranchCountQuery, Result<int>>([validator]);
            RequestHandlerDelegate<Result<int>> next = (_) => throw new UnreachableException("Next must not run when validation fails.");

            var result = await behavior.Handle(new TestBranchCountQuery(4), next, CancellationToken.None);

            var validationError = Assert.IsType<ValidationError>(result.Error);
            Assert.True(result.IsFailure);
            Assert.Equal(ErrorType.Validation, validationError.Type);
            Assert.Equal(new[] { new FieldError("KnownBranches", "One or more branches are required.") }, validationError.Errors);
        }

        [Fact]
        public async Task Send_InvalidCommandThroughRegisteredPipeline_FailsValidation_AndSkipsHandler()
        {
            var handler = new TestPingCommandHandler();
            var validator = new TestValidator<TestPingCommand>().WithFailure("Name", "A name is required.");
            using var provider = new ServiceCollection()
                .AddApplication()
                .AddSingleton<IRequestHandler<TestPingCommand, Result>>(handler)
                .AddSingleton<IValidator<TestPingCommand>>(validator)
                .BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var result = await mediator.Send(new TestPingCommand());

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorType.Validation, result.Error!.Type);
            Assert.False(handler.WasHandled);
        }
    }

    public sealed class TestValidator<TRequest> : AbstractValidator<TRequest>
    {
        private readonly List<(string PropertyName, string Message)> injectedFailures = [];

        public TestValidator()
        {
            RuleFor(request => request).Custom((request, context) =>
            {
                foreach (var (propertyName, message) in injectedFailures)
                {
                    context.AddFailure(propertyName, message);
                }
            });
        }

        public TestValidator<TRequest> WithFailure(string propertyName, string message)
        {
            injectedFailures.Add((propertyName, message));
            return this;
        }
    }
}
