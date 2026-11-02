using System.Threading;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.CafeProfile;
using CafeHub.Application.CafeProfile.Contracts;
using CafeHub.Application.CafeProfile.DTOs;
using CafeHub.Application.CafeProfile.Queries;

namespace CafeHub.UnitTests.CafeProfile.Queries
{
    public sealed class GetCafeProfileQueryHandlerTests
    {
        private static readonly CafeProfileResponse AResponse = new()
        {
            CafeName = "My Cafe",
            LogoMediaReference = "media-ref-123",
            PrimaryColor = "#ff0000",
            SecondaryColor = "#00ff00",
            AccentColor = "#0000ff",
            ContactPhone = "+1-555-0100",
            ContactEmail = "contact@example.com",
            ContactAddress = "123 Main St",
            Timezone = "America/New_York",
            ConcurrencyToken = "12345",
        };

        [Fact]
        public async Task Handle_WithSettingsRow_ReturnsTheResponse()
        {
            var handler = new GetCafeProfileQueryHandler(new FakeCafeSettingsReader(AResponse));

            var result = await handler.Handle(new GetCafeProfileQuery(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Same(AResponse, result.Value);
        }

        [Fact]
        public async Task Handle_MissingRow_ReturnsCafeProfileNotFound()
        {
            var handler = new GetCafeProfileQueryHandler(new FakeCafeSettingsReader(null));

            var result = await handler.Handle(new GetCafeProfileQuery(), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CafeProfileErrors.NotFoundCode, result.Error!.Code);
            Assert.Equal(CafeProfileErrors.NotFoundMessage, result.Error.Message);
        }

        private sealed class FakeCafeSettingsReader : ICafeSettingsReader
        {
            private readonly CafeProfileResponse? _response;

            public FakeCafeSettingsReader(CafeProfileResponse? response)
            {
                _response = response;
            }

            public Task<CafeProfileResponse?> GetAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(_response);
            }
        }
    }
}
