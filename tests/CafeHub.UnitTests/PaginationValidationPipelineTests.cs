using CafeHub.Application;
using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Pagination;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests
{
    public sealed class PaginationValidationPipelineTests
    {
        [Fact]
        public async Task Send_InvalidPagedListQuery_FailsValidation_WithPaginationFieldErrors_AndSkipsHandler()
        {
            var handler = new TestPagedListQueryHandler();
            using var provider = new ServiceCollection()
                .AddApplication()
                .AddSingleton<IRequestHandler<TestPagedListQuery, Result<string>>, TestPagedListQueryHandler>()
                .AddSingleton<IValidator<TestPagedListQuery>, TestPagedListQueryValidator>()
                .BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var result = await mediator.Send(new TestPagedListQuery(new OffsetPageRequest { Page = 1, PageSize = 0 }));

            Assert.True(result.IsFailure);
            var validationError = Assert.IsType<ValidationError>(result.Error);
            Assert.Equal(ErrorType.Validation, validationError.Type);
            Assert.Equal(new[] { new FieldError("pageSize", "The page size must be at least 1.") }, validationError.Errors);
            Assert.False(handler.WasHandled);
        }

        [Fact]
        public async Task Send_ValidPagedListQuery_ReachesTheHandler()
        {
            var handler = new TestPagedListQueryHandler();
            using var provider = new ServiceCollection()
                .AddApplication()
                .AddSingleton<IRequestHandler<TestPagedListQuery, Result<string>>>(handler)
                .AddSingleton<IValidator<TestPagedListQuery>, TestPagedListQueryValidator>()
                .BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var result = await mediator.Send(new TestPagedListQuery(new OffsetPageRequest { Page = 3, PageSize = 25 }));

            Assert.True(result.IsSuccess);
            Assert.Equal("paged-list", result.Value);
            Assert.True(handler.WasHandled);
        }
    }

    public sealed record TestPagedListQuery(OffsetPageRequest PageRequest) : IQuery<string>;

    public sealed class TestPagedListQueryHandler : IQueryHandler<TestPagedListQuery, string>
    {
        public bool WasHandled { get; private set; }

        public Task<Result<string>> Handle(TestPagedListQuery query, CancellationToken cancellationToken)
        {
            WasHandled = true;

            return Task.FromResult(Result.Success("paged-list"));
        }
    }

    public sealed class TestPagedListQueryValidator : AbstractValidator<TestPagedListQuery>
    {
        public TestPagedListQueryValidator(PaginationOptions options)
        {
            RuleFor(query => query.PageRequest).SetValidator(new OffsetPageRequestValidator(options));
        }
    }
}
