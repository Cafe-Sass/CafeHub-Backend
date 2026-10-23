using System.Text.Json;
using CafeHub.Application.Pagination;

namespace CafeHub.UnitTests
{
    public sealed class PaginationPageTests
    {
        [Fact]
        public void OffsetPage_TrimsFetchedItems_ToPageSize_AndDerivesHasNextPage()
        {
            var fetchedItems = Enumerable.Range(0, 21).Select(index => $"row-{index}").ToList();

            var page = OffsetPage<string>.Create(fetchedItems, page: 2, pageSize: 20, totalCount: null);

            Assert.Equal(Enumerable.Range(0, 20).Select(index => $"row-{index}"), page.Items);
            Assert.Equal(2, page.Page);
            Assert.Equal(20, page.PageSize);
            Assert.True(page.HasNextPage);
            Assert.Null(page.TotalCount);
        }

        [Fact]
        public void OffsetPage_WhenNoNextPage_DerivesFalse_KeepsEveryFetchedItem()
        {
            var fetchedItems = Enumerable.Range(0, 7).Select(index => $"row-{index}").ToList();

            var page = OffsetPage<string>.Create(fetchedItems, page: 1, pageSize: 20, totalCount: null);

            Assert.Equal(7, page.Items.Count);
            Assert.False(page.HasNextPage);
            Assert.Null(page.TotalCount);
        }

        [Fact]
        public void OffsetPage_TotalCount_IsPopulatedOnlyWhenRequested()
        {
            var fetchedItems = Enumerable.Range(0, 21).Select(index => $"row-{index}").ToList();

            var unrequestedPage = OffsetPage<string>.Create(fetchedItems, page: 1, pageSize: 20, totalCount: null);
            var requestedPage = OffsetPage<string>.Create(fetchedItems, page: 1, pageSize: 20, totalCount: 42);

            Assert.Null(unrequestedPage.TotalCount);
            Assert.Equal(42, requestedPage.TotalCount);
        }

        [Fact]
        public void OffsetPage_TotalCount_IsOmittedFromSerializedOutput_WhenNotRequested()
        {
            var fetchedItems = Enumerable.Range(0, 4).Select(index => $"row-{index}").ToList();
            var page = OffsetPage<string>.Create(fetchedItems, page: 1, pageSize: 20, totalCount: null);

            var serialized = JsonSerializer.Serialize(page);

            Assert.DoesNotContain("totalcount", serialized, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void OffsetPage_Create_Throws_WhenThePageIsBelowOne()
        {
            var exception = Record.Exception(
                () => OffsetPage<string>.Create(["row-1"], page: 0, pageSize: 20, totalCount: null));

            Assert.IsType<InvalidOperationException>(exception);
        }

        [Fact]
        public void KeysetPage_NextCursor_IsNull_WhenNoNextPageExists()
        {
            var fetchedItems = Enumerable.Range(0, 20).Select(index => $"row-{index}").ToList();

            var page = KeysetPage<string>.Create(fetchedItems, pageSize: 20, nextAnchor: null);

            Assert.Equal(20, page.PageSize);
            Assert.False(page.HasNextPage);
            Assert.Null(page.NextCursor);
        }

        [Fact]
        public void KeysetPage_BuildsTheNextCursor_OnlyWhenAPageFollows()
        {
            var (fingerprint, anchor) = CreateCursorMaterial();
            var fetchedItems = Enumerable.Range(0, 21).Select(index => $"row-{index}").ToList();

            var page = KeysetPage<string>.Create(fetchedItems, pageSize: 20, nextAnchor: anchor);

            Assert.Equal(20, page.Items.Count);
            Assert.True(page.HasNextPage);
            Assert.NotNull(page.NextCursor);
            var decodedAnchor = CursorCodec.Decode(page.NextCursor!, fingerprint);
            Assert.True(decodedAnchor.IsSuccess);
            Assert.Equal(anchor.LastId, decodedAnchor.Value!.LastId);
        }

        [Fact]
        public void KeysetPage_Create_Throws_WhenTheAnchorPresenceContradictsThePageSizePlusOneRule()
        {
            var (fingerprint, anchor) = CreateCursorMaterial();
            var fetchedItems = Enumerable.Range(0, 19).Select(index => $"row-{index}").ToList();

            var exception = Record.Exception(
                () => KeysetPage<string>.Create(fetchedItems, pageSize: 20, nextAnchor: anchor));

            Assert.IsType<InvalidOperationException>(exception);
        }

        private static (SortFingerprint Fingerprint, CursorPage Anchor) CreateCursorMaterial()
        {
            var fingerprint = SortFingerprint.From(
            [
                new SortTerm("recorded_at", SortDirection.Desc),
                new SortTerm("id", SortDirection.Desc),
            ]);

            return (fingerprint, new CursorPage(fingerprint, ["2026-10-21T05:00:00.000000Z"], Guid.NewGuid()));
        }
    }
}
