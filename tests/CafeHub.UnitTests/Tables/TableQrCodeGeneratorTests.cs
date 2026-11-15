using CafeHub.Application.Tables.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Tables;

namespace CafeHub.UnitTests.Tables
{
    public sealed class TableQrCodeGeneratorTests
    {
        private sealed class FixedQrCodeGenerator : ITableQrCodeGenerator
        {
            public string Generate() => "Abcdefghijklmnopqrstuv";
        }

        [Fact]
        public void Generate_ReturnsTheFixedCodeFromTheDeterministicFake()
        {
            ITableQrCodeGenerator generator = new FixedQrCodeGenerator();

            Assert.Equal("Abcdefghijklmnopqrstuv", generator.Generate());
        }

        [Fact]
        public void Generate_ReturnsFixedLengthUrlSafeCodes()
        {
            var generator = new TableQrCodeGenerator();

            var code = generator.Generate();

            Assert.Equal(BranchTable.QrCodeLength, code.Length);
            Assert.Matches("^[A-Za-z0-9_-]{22}$", code);
        }

        [Fact]
        public void Generate_ProducesDifferentCodes()
        {
            var generator = new TableQrCodeGenerator();

            var first = generator.Generate();
            var second = generator.Generate();

            Assert.NotEqual(first, second);
            Assert.Equal(BranchTable.QrCodeLength, first.Length);
            Assert.Equal(BranchTable.QrCodeLength, second.Length);
        }
    }
}
