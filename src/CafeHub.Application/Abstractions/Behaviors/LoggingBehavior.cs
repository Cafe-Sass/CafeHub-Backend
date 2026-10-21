using System.Diagnostics;
using CafeHub.Application.Abstractions.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CafeHub.Application.Abstractions.Behaviors
{
    public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
        where TResponse : Result
    {
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> logger;

        public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var startTimestamp = Stopwatch.GetTimestamp();
            var requestName = typeof(TRequest).Name;

            try
            {
                var result = await next(cancellationToken);
                var duration = Stopwatch.GetElapsedTime(startTimestamp);

                if (result.IsSuccess)
                {
                    logger.LogInformation("Request {RequestName} succeeded in {Duration}", requestName, duration);
                }
                else
                {
                    var error = result.Error!;
                    logger.LogWarning("Request {RequestName} failed with code {ErrorCode} of type {ErrorType} in {Duration}", requestName, error.Code, error.Type, duration);
                }

                return result;
            }
            catch (Exception exception)
            {
                var duration = Stopwatch.GetElapsedTime(startTimestamp);
                logger.LogError(exception, "Request {RequestName} threw {ExceptionType} in {Duration}", requestName, exception.GetType().Name, duration);
                throw;
            }
        }
    }
}
