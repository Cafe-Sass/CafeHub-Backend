using CafeHub.WebApi.Correlation;

namespace CafeHub.UnitTests
{
    public sealed class GuidCorrelationIdGeneratorTests
    {
        [Fact]
        public void Generate_CreatesAValueTheCorrelationIdPolicyAccepts()
        {
            Assert.True(CorrelationIdPolicy.IsValid(new GuidCorrelationIdGenerator().Generate()));
        }

        [Fact]
        public void Generate_CreatesDistinct32LowercaseHexadecimalValues_InTheGuidFormat()
        {
            var generator = new GuidCorrelationIdGenerator();
            var first = generator.Generate();
            var second = generator.Generate();

            Assert.Equal(32, first.Length);
            Assert.NotEqual(first, second);
            Assert.All(first, character => Assert.True("0123456789abcdef".Contains(character)));
        }
    }
}
