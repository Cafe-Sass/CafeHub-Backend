namespace CafeHub.WebApi.Security
{
    internal sealed class TableQrResponseMiddleware(RequestDelegate next)
    {
        public Task InvokeAsync(HttpContext context)
        {
            if (context.GetEndpoint()?.Metadata.GetMetadata<TableQrEndpointAttribute>() is not null)
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers.CacheControl = SecurityHeaderValues.CacheControlValue;
                    return Task.CompletedTask;
                });
            }

            return next(context);
        }
    }
}
