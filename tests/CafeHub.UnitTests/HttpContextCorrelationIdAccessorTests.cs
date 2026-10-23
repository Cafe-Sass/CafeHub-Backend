using CafeHub.WebApi.Correlation;
using Microsoft.AspNetCore.Http;

namespace CafeHub.UnitTests
{
    public sealed class HttpContextCorrelationIdAccessorTests
    {
        [Fact]
        public void CorrelationId_ReturnsTheIdentifierStoredForTheCurrentRequest()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Items[CorrelationIdConstants.ItemKey] = "corr-stored-1";
            var accessor = new HttpContextCorrelationIdAccessor(new HttpContextAccessor { HttpContext = httpContext });

            Assert.Equal("corr-stored-1", accessor.CorrelationId);
        }

        [Fact]
        public void CorrelationId_ReturnsNull_WhenNoHttpContextIsActive()
        {
            var accessor = new HttpContextCorrelationIdAccessor(new HttpContextAccessor());

            Assert.Null(accessor.CorrelationId);
        }

        [Fact]
        public void CorrelationId_ReturnsNull_WhenNoIdentifierWasStoredForTheRequest()
        {
            var httpContext = new DefaultHttpContext();
            var accessor = new HttpContextCorrelationIdAccessor(new HttpContextAccessor { HttpContext = httpContext });

            Assert.Null(accessor.CorrelationId);
        }
    }
}
