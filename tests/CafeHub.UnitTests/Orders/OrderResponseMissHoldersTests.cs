using CafeHub.Application.Orders;

namespace CafeHub.UnitTests.Orders
{
    public sealed class OrderResponseMissHoldersTests
    {
        private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid OtherUserId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        private static readonly Guid StationOne = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly Guid StationTwo = Guid.Parse("55555555-5555-5555-5555-555555555555");

        [Fact]
        public void StaffWithoutStationAssignmentsAreAlwaysEligible()
        {
            var holders = OrderResponseMissHolders.Resolve(
                [new MissHolderCandidate(UserId, null)],
                [StationOne]);

            Assert.Equal([UserId], holders);
        }

        [Fact]
        public void StaffWithAMatchingStationIsEligible()
        {
            var holders = OrderResponseMissHolders.Resolve(
                [new MissHolderCandidate(UserId, [StationOne])],
                [StationOne, StationTwo]);

            Assert.Equal([UserId], holders);
        }

        [Fact]
        public void StaffWithoutAMatchingStationIsExcludedWhenEveryItemIsRouted()
        {
            var holders = OrderResponseMissHolders.Resolve(
                [new MissHolderCandidate(UserId, [StationOne])],
                [StationTwo]);

            Assert.Empty(holders);
        }

        [Fact]
        public void AnUnroutedItemBelongsToEveryAssignedStaff()
        {
            var holders = OrderResponseMissHolders.Resolve(
                [new MissHolderCandidate(UserId, [StationOne])],
                [StationTwo, null]);

            Assert.Equal([UserId], holders);
        }

        [Fact]
        public void EmptyInputsProduceNoHolders()
        {
            Assert.Empty(OrderResponseMissHolders.Resolve([], [StationOne]));
            Assert.Empty(OrderResponseMissHolders.Resolve([new MissHolderCandidate(UserId, null)], []));
        }

        [Fact]
        public void DuplicateCandidatesCollapseToDistinctUserIds()
        {
            var holders = OrderResponseMissHolders.Resolve(
                [new MissHolderCandidate(UserId, null), new MissHolderCandidate(UserId, [StationTwo]), new MissHolderCandidate(OtherUserId, null)],
                [StationOne]);

            Assert.Equal([UserId, OtherUserId], holders);
        }
    }
}
