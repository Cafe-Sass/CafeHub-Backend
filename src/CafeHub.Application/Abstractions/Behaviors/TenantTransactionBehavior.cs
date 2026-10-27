using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.Application.Abstractions.Behaviors
{
    public sealed class TenantTransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
        where TResponse : Result
    {
        private readonly IServiceProvider serviceProvider;

        public TenantTransactionBehavior(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (request is not ITenantTransactionalCommand)
            {
                return await next(cancellationToken);
            }

            var unitOfWork = serviceProvider.GetRequiredService<ITenantUnitOfWork>();

            await unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await next(cancellationToken);
                if (result.IsSuccess)
                {
                    await unitOfWork.CommitTransactionAsync(cancellationToken);
                }
                else
                {
                    await unitOfWork.RollbackTransactionAsync(cancellationToken);
                }

                return result;
            }
            catch (Exception)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
