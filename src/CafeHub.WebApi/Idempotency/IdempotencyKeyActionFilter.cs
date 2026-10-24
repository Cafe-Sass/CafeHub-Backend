using CafeHub.Application.Idempotency;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CafeHub.WebApi.Idempotency
{
    public sealed class IdempotencyKeyActionFilter : IAsyncActionFilter
    {
        private const string HeaderName = "Idempotency-Key";

        public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var headerValues))
            {
                var headerValue = headerValues.Count > 0 ? headerValues[^1] : null;
                foreach (var command in context.ActionArguments.Values.OfType<IIdempotentCommand>())
                {
                    command.IdempotencyKey = headerValue;
                }
            }

            return next();
        }
    }
}
