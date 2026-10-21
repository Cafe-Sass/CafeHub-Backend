using System.Reflection;
using CafeHub.Application.Abstractions.Results;
using FluentValidation;
using MediatR;

namespace CafeHub.Application.Abstractions.Behaviors
{
    public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
        where TResponse : Result
    {
        private static readonly Func<Error, TResponse> CreateFailedResult = BuildFailedResultFactory();

        private readonly IEnumerable<IValidator<TRequest>> validators;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            this.validators = validators ?? throw new ArgumentNullException(nameof(validators));
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (!validators.Any())
            {
                return await next(cancellationToken);
            }

            var fieldErrors = new List<FieldError>();
            foreach (var validator in validators)
            {
                var validationResult = await validator.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken);
                foreach (var failure in validationResult.Errors)
                {
                    fieldErrors.Add(new FieldError(failure.PropertyName, failure.ErrorMessage));
                }
            }

            if (fieldErrors.Count == 0)
            {
                return await next(cancellationToken);
            }

            return CreateFailedResult(ValidationError.Create(fieldErrors));
        }

        private static Func<Error, TResponse> BuildFailedResultFactory()
        {
            if (typeof(TResponse) == typeof(Result))
            {
                return error => (TResponse)(object)Result.Failure(error);
            }

            var responseType = typeof(TResponse);
            if (!responseType.IsGenericType || responseType.GetGenericTypeDefinition() != typeof(Result<>))
            {
                throw new InvalidOperationException($"Cannot build a failed result for the response type {typeof(TResponse)}.");
            }

            var valueType = responseType.GetGenericArguments()[0];
            var failureMethod = typeof(Result)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == nameof(Result.Failure) && method.IsGenericMethodDefinition)
                .MakeGenericMethod(valueType);

            return failureMethod.CreateDelegate<Func<Error, TResponse>>();
        }
    }
}
