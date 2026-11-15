using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Stations
{
    public sealed class StationTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);

        [Fact]
        public void Create_TrimsTheNameAndStartsActive()
        {
            var station = Station.Create(Guid.NewGuid(), Guid.NewGuid(), "  Bar  ", 2, Now);

            Assert.Equal("Bar", station.Name);
            Assert.Equal(StationStatus.Active, station.Status);
            Assert.Equal(2, station.SortOrder);
        }

        [Theory]
        [InlineData(64, true)]
        [InlineData(65, false)]
        public void Create_EnforcesTheNameLengthAfterTrimming(int length, bool accepted)
        {
            var branchId = Guid.NewGuid();
            var name = new string('a', length);

            if (accepted)
            {
                Assert.Equal(name, Station.Create(Guid.NewGuid(), branchId, $"  {name}  ", 0, Now).Name);
                return;
            }

            Assert.Throws<ArgumentException>(() => Station.Create(Guid.NewGuid(), branchId, name, 0, Now));
        }

        [Theory]
        [InlineData("   ")]
        [InlineData("\t")]
        [InlineData("Bar\u0000")]
        [InlineData("Kit\u0007chen")]
        public void Create_RejectsBlankAndControlCharacters(string name)
        {
            Assert.Throws<ArgumentException>(() => Station.Create(Guid.NewGuid(), Guid.NewGuid(), name, 0, Now));
        }

        [Fact]
        public void Create_RejectsANegativeSortOrder()
        {
            Assert.Throws<ArgumentException>(
                () => Station.Create(Guid.NewGuid(), Guid.NewGuid(), "Bar", -1, Now));
        }

        [Fact]
        public void DeactivateAndReactivate_ChangeOnlyTheStatusAndKeepTheRow()
        {
            var station = Station.Create(Guid.NewGuid(), Guid.NewGuid(), "Bar", 0, Now);
            var actor = Guid.NewGuid();
            var later = Now.AddMinutes(5);

            station.Deactivate(later, actor);

            Assert.Equal(StationStatus.Inactive, station.Status);
            Assert.Equal(later, station.UpdatedAt);
            Assert.Equal(actor, station.UpdatedBy);

            station.Reactivate(later.AddMinutes(5), actor);

            Assert.Equal(StationStatus.Active, station.Status);
            Assert.Equal("Bar", station.Name);
        }
    }
}
