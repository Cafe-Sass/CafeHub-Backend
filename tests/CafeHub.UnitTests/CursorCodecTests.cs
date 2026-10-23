using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Pagination;

namespace CafeHub.UnitTests
{
    public sealed class CursorCodecTests
    {
        private static (SortFingerprint Fingerprint, CursorPage Anchor) CreateAnchor(
            string recordedAt,
            Guid? lastId = null)
        {
            var fingerprint = SortFingerprint.From(
            [
                new SortTerm("recorded_at", SortDirection.Desc),
                new SortTerm("id", SortDirection.Desc),
            ]);
            var anchor = new CursorPage(
                fingerprint,
                [recordedAt],
                lastId ?? new Guid("01933a2e-7b1a-7dd1-9c4e-9f2b41c6b8a1"));

            return (fingerprint, anchor);
        }

        [Fact]
        public void Encode_Decode_RoundTripsTheSameAnchorPayload()
        {
            var (fingerprint, anchor) = CreateAnchor("2026-10-21T05:00:00.000000Z");

            var cursor = CursorCodec.Encode(anchor);
            var decoded = CursorCodec.Decode(cursor, fingerprint);

            Assert.True(decoded.IsSuccess);
            Assert.Equal(anchor.LastId, decoded.Value!.LastId);
            Assert.Equal(new[] { "2026-10-21T05:00:00.000000Z" }, decoded.Value!.SortValues);
            Assert.Equal(fingerprint, decoded.Value!.Fingerprint);
        }

        [Fact]
        public void Encode_Throws_WhenTheAnchorIdentifierIsEmpty()
        {
            var (fingerprint, _) = CreateAnchor("2026-10-21T05:00:00.000000Z");

            var exception = Record.Exception(() => CursorCodec.Encode(new CursorPage(fingerprint, [], Guid.Empty)));

            Assert.IsType<ArgumentException>(exception);
        }

        [Fact]
        public void Decode_RejectsAnEmptyCursor_WithTheCursorInvalidCode()
        {
            var (fingerprint, _) = CreateAnchor("2026-10-21T05:00:00.000000Z");

            var decoded = CursorCodec.Decode(" ", fingerprint);

            Assert.True(decoded.IsFailure);
            Assert.Equal(PaginationErrors.CursorInvalidCode, decoded.Error!.Code);
        }

        [Fact]
        public void Decode_RejectsACursorAboveTheMaximumLength()
        {
            var (fingerprint, _) = CreateAnchor("2026-10-21T05:00:00.000000Z");
            var oversizedCursor = new string('C', CursorCodec.MaxCursorLength + 1);

            var decoded = CursorCodec.Decode(oversizedCursor, fingerprint);

            Assert.True(decoded.IsFailure);
            Assert.Equal(PaginationErrors.CursorInvalidCode, decoded.Error!.Code);
        }

        [Theory]
        [InlineData("not base64url at all!!!")]
        [InlineData("a+standardBase64")]
        [InlineData("a/slashEncoded")]
        [InlineData("padding=Present")]
        public void Decode_RejectsInvalidBase64UrlText(string invalidCursor)
        {
            var (fingerprint, _) = CreateAnchor("2026-10-21T05:00:00.000000Z");

            var decoded = CursorCodec.Decode(invalidCursor, fingerprint);

            Assert.True(decoded.IsFailure);
            Assert.Equal(PaginationErrors.CursorInvalidCode, decoded.Error!.Code);
        }

        [Fact]
        public void Decode_RejectsMalformedPayloadJson()
        {
            var (fingerprint, _) = CreateAnchor("2026-10-21T05:00:00.000000Z");

            var decoded = CursorCodec.Decode(EncodeTestPayload("not-json-at-all"), fingerprint);

            Assert.True(decoded.IsFailure);
            Assert.Equal(PaginationErrors.CursorInvalidCode, decoded.Error!.Code);
        }

        [Fact]
        public void Decode_RejectsUnexpectedPayloadMembers()
        {
            var (fingerprint, _) = CreateAnchor("2026-10-21T05:00:00.000000Z");
            var extraMemberJson =
                "{\"v\":1,\"s\":\"recorded_at:Desc|id:Desc\",\"k\":[\"2026-10-21T05:00:00.000000Z\"],\"i\":\"01933a2e-7b1a-7dd1-9c4e-9f2b41c6b8a1\",\"extra\":true}";

            var decoded = CursorCodec.Decode(EncodeTestPayload(extraMemberJson), fingerprint);

            Assert.True(decoded.IsFailure);
            Assert.Equal(PaginationErrors.CursorInvalidCode, decoded.Error!.Code);
        }

        [Fact]
        public void Decode_RejectsMissingPayloadMembers()
        {
            var (fingerprint, _) = CreateAnchor("2026-10-21T05:00:00.000000Z");
            var missingMemberJson = "{\"v\":1,\"s\":\"recorded_at:Desc|id:Desc\",\"k\":[\"2026-10-21T05:00:00.000000Z\"]}";

            var decoded = CursorCodec.Decode(EncodeTestPayload(missingMemberJson), fingerprint);

            Assert.True(decoded.IsFailure);
            Assert.Equal(PaginationErrors.CursorInvalidCode, decoded.Error!.Code);
        }

        [Fact]
        public void Decode_RejectsAnUnknownPayloadVersion()
        {
            var (fingerprint, _) = CreateAnchor("2026-10-21T05:00:00.000000Z");
            var unknownVersionJson =
                "{\"v\":99,\"s\":\"recorded_at:Desc|id:Desc\",\"k\":[\"2026-10-21T05:00:00.000000Z\"],\"i\":\"01933a2e-7b1a-7dd1-9c4e-9f2b41c6b8a1\"}";

            var decoded = CursorCodec.Decode(EncodeTestPayload(unknownVersionJson), fingerprint);

            Assert.True(decoded.IsFailure);
            Assert.Equal(PaginationErrors.CursorInvalidCode, decoded.Error!.Code);
        }

        [Fact]
        public void Decode_RejectsAMalformedIdentifier()
        {
            var (fingerprint, _) = CreateAnchor("2026-10-21T05:00:00.000000Z");
            var malformedIdJson =
                "{\"v\":1,\"s\":\"recorded_at:Desc|id:Desc\",\"k\":[\"2026-10-21T05:00:00.000000Z\"],\"i\":\"not-a-uuid\"}";

            var decoded = CursorCodec.Decode(EncodeTestPayload(malformedIdJson), fingerprint);

            Assert.True(decoded.IsFailure);
            Assert.Equal(PaginationErrors.CursorInvalidCode, decoded.Error!.Code);
        }

        [Fact]
        public void Decode_RejectsASecondSortFingerprint()
        {
            var (fingerprint, anchor) = CreateAnchor("2026-10-21T05:00:00.000000Z");
            var cursor = CursorCodec.Encode(anchor);
            var otherFingerprint = SortFingerprint.From([new SortTerm("recorded_at", SortDirection.Asc)]);

            var decoded = CursorCodec.Decode(cursor, otherFingerprint);

            Assert.True(decoded.IsFailure);
            Assert.Equal(PaginationErrors.CursorInvalidCode, decoded.Error!.Code);
        }

        [Fact]
        public void Decode_RejectsANonCanonicalEncoding_CarryingTheSameDecodedPayload()
        {
            var (fingerprint, _) = CreateAnchor("2026-10-21T05:00:00.000000Z");

            CursorPage? anchor = null;
            var canonicalCursor = string.Empty;
            foreach (var valueLength in Enumerable.Range(0, 9))
            {
                var candidateAnchor = new CursorPage(fingerprint, [new string('r', valueLength)], new Guid("01933a2e-7b1a-7dd1-9c4e-9f2b41c6b8a1"));
                var candidateCursor = CursorCodec.Encode(candidateAnchor);
                if (candidateCursor.Length % 4 != 0)
                {
                    anchor = candidateAnchor;
                    canonicalCursor = candidateCursor;
                    break;
                }
            }

            Assert.NotNull(anchor);
            var tamperedCursor = FlipAPairwiseEquivalentTrailingCharacter(canonicalCursor);

            var decoded = CursorCodec.Decode(tamperedCursor, fingerprint);

            Assert.True(decoded.IsFailure);
            Assert.Equal(PaginationErrors.CursorInvalidCode, decoded.Error!.Code);
        }

        [Fact]
        public void Decode_FailureMessages_NeverEchoTheRejectedCursorText()
        {
            var otherFingerprint = SortFingerprint.From([new SortTerm("recorded_at", SortDirection.Asc)]);

            var (fingerprint, markedAnchor) = CreateAnchor("2026-10-21CAFEHUB-MARKER-06T05:00:00.000000Z");
            var markedCursor = CursorCodec.Encode(markedAnchor);
            var invalidInputCursor = "=/+notbase64";
            var invalidJsonInput = EncodeTestPayload("not-json-at-all");
            var oversizedInput = new string('C', CursorCodec.MaxCursorLength + 1);

            var emptyRejection = CursorCodec.Decode(" ", fingerprint);
            var oversizedRejection = CursorCodec.Decode(oversizedInput, fingerprint);
            var base64Rejection = CursorCodec.Decode(invalidInputCursor, fingerprint);
            var jsonRejection = CursorCodec.Decode(invalidJsonInput, fingerprint);
            var fingerprintRejection = CursorCodec.Decode(markedCursor, otherFingerprint);

            Assert.True(emptyRejection.IsFailure);
            Assert.Equal("The cursor is invalid: it must be set when resuming a page", emptyRejection.Error!.Message);

            Assert.True(oversizedRejection.IsFailure);
            Assert.Equal(PaginationErrors.CursorInvalidCode, oversizedRejection.Error!.Code);
            Assert.DoesNotContain(oversizedInput, oversizedRejection.Error!.Message, StringComparison.Ordinal);

            Assert.True(base64Rejection.IsFailure);
            Assert.Equal(PaginationErrors.CursorInvalidCode, base64Rejection.Error!.Code);
            Assert.DoesNotContain(invalidInputCursor, base64Rejection.Error!.Message, StringComparison.Ordinal);

            Assert.True(jsonRejection.IsFailure);
            Assert.Equal(PaginationErrors.CursorInvalidCode, jsonRejection.Error!.Code);
            Assert.DoesNotContain(invalidJsonInput, jsonRejection.Error!.Message, StringComparison.Ordinal);

            Assert.True(fingerprintRejection.IsFailure);
            Assert.Equal(PaginationErrors.CursorInvalidCode, fingerprintRejection.Error!.Code);
            Assert.DoesNotContain(markedCursor, fingerprintRejection.Error!.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("CAFEHUB-MARKER-06", fingerprintRejection.Error!.Message, StringComparison.Ordinal);
        }

        private static string FlipAPairwiseEquivalentTrailingCharacter(string cursor)
        {
            const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
            var lastCharacterIndex = Alphabet.IndexOf(cursor[^1]);
            var offset = lastCharacterIndex < 16 ? 16 : -16;
            return cursor[..^1] + Alphabet[(lastCharacterIndex + offset) % Alphabet.Length];
        }

        private static string EncodeTestPayload(string payloadJson)
        {
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payloadJson))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
