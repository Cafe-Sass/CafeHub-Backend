using System.Text.Json;
using System.Text.Json.Serialization;
using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Pagination
{
    /// <summary>
    /// The strict opaque cursor codec for keyset paging. Proposed format, pending owner confirmation: a versioned
    /// payload (version number, sort fingerprint, the anchor row's sort values as strings, and the anchor row's
    /// identifier) serialized to compact JSON and encoded as base64url without padding, capped at
    /// <see cref="MaxCursorLength"/> characters.
    /// <para>
    /// Decoding is strict and rejects every malformed or tampered cursor with the
    /// <see cref="PaginationErrors.CursorInvalidCode"/> validation error — never an exception to the caller — when the
    /// text is not valid base64url, exceeds the maximum length, is invalid JSON or carries extra or missing members,
    /// carries an unknown version, carries a missing or empty identifier, was issued for a different sort than the
    /// expected fingerprint, or is not in canonical form (re-encoding the decoded payload must reproduce the input
    /// exactly). Decoding rejects a non-canonical encoding already during the base64url translation by refusing
    /// non-zero unused trailing bits.
    /// </para>
    /// <para>
    /// The cursor is NOT cryptographically signed: it protects only against malformed and inconsistent input, and
    /// every query must still apply its tenant and authorization filters, so a forged cursor can never widen access.
    /// Rejected alternative: HMAC-signed cursors, which add a secret and key rotation no story requires yet.
    /// </para>
    /// </summary>
    public static class CursorCodec
    {
        /// <summary>Maximum accepted cursor length; proposed 512, pending owner confirmation.</summary>
        public const int MaxCursorLength = 512;

        private const int PayloadVersion = 1;
        private const string Base64UrlAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

        public static Result<CursorPage> Decode(string? cursor, SortFingerprint expectedFingerprint)
        {
            if (expectedFingerprint is null)
            {
                throw new ArgumentNullException(nameof(expectedFingerprint));
            }

            if (string.IsNullOrWhiteSpace(cursor))
            {
                return Invalid("it must be set when resuming a page");
            }

            if (cursor.Length > MaxCursorLength)
            {
                return Invalid($"it exceeds the maximum length of {MaxCursorLength} characters");
            }

            if (!TryDecodeBase64Url(cursor, out var payloadBytes))
            {
                return Invalid("it is not valid base64url text");
            }

            CursorPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<CursorPayload>(payloadBytes);
            }
            catch (JsonException)
            {
                return Invalid("its payload is not valid JSON");
            }

            if (payload is null)
            {
                return Invalid("its payload is not valid JSON");
            }

            if (payload.Version != PayloadVersion)
            {
                return Invalid("it carries an unknown version");
            }

            if (string.IsNullOrEmpty(payload.Sort))
            {
                return Invalid("its sort fingerprint is missing");
            }

            if (payload.SortValues is null || payload.SortValues.Contains(null!))
            {
                return Invalid("its sort values are missing");
            }

            if (!string.Equals(payload.Sort, expectedFingerprint.Value, StringComparison.Ordinal))
            {
                return Invalid("it was issued for a different sort");
            }

            if (payload.LastId == Guid.Empty)
            {
                return Invalid("its identifier must not be empty");
            }

            if (!string.Equals(EncodeText(payload), cursor, StringComparison.Ordinal))
            {
                return Invalid("it is not in canonical form");
            }

            return Result.Success(new CursorPage(expectedFingerprint, payload.SortValues, payload.LastId));
        }

        public static string Encode(CursorPage anchor)
        {
            if (anchor is null)
            {
                throw new ArgumentNullException(nameof(anchor));
            }

            if (anchor.SortValues.Contains(null!))
            {
                throw new ArgumentException("The cursor anchor sort values must not contain null.", nameof(anchor));
            }

            if (anchor.LastId == Guid.Empty)
            {
                throw new InvalidOperationException("The cursor anchor identifier must not be empty.");
            }

            var payload = new CursorPayload
            {
                Version = PayloadVersion,
                Sort = anchor.Fingerprint.Value,
                SortValues = [.. anchor.SortValues],
                LastId = anchor.LastId,
            };

            var cursor = EncodeText(payload);
            if (cursor.Length > MaxCursorLength)
            {
                throw new InvalidOperationException(
                    $"The cursor for the provided anchor exceeds the maximum length of {MaxCursorLength} characters.");
            }

            return cursor;
        }

        private static Result<CursorPage> Invalid(string reason)
        {
            return Result.Failure<CursorPage>(PaginationErrors.CursorIsInvalid(reason));
        }

        private static bool TryDecodeBase64Url(string cursor, out byte[] bytes)
        {
            bytes = [];
            var remainder = cursor.Length % 4;
            if (remainder == 1)
            {
                return false;
            }

            bytes = new byte[cursor.Length / 4 * 3 + (remainder == 2 ? 1 : remainder == 3 ? 2 : 0)];
            var buffer = 0;
            var bitCount = 0;
            var byteIndex = 0;
            foreach (var character in cursor)
            {
                var value = DecodeCharacter(character);
                if (value < 0)
                {
                    return false;
                }

                buffer = (buffer << 6) | value;
                bitCount += 6;
                if (bitCount < 8)
                {
                    continue;
                }

                bitCount -= 8;
                bytes[byteIndex++] = (byte)((buffer >> bitCount) & 0xFF);
                buffer &= (1 << bitCount) - 1;
            }

            return buffer == 0;
        }

        private static int DecodeCharacter(char character)
        {
            var index = Base64UrlAlphabet.IndexOf(character);
            return index;
        }

        private static string EncodeText(CursorPayload payload)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
        private sealed record CursorPayload
        {
            [JsonPropertyName("v")]
            public required int Version { get; init; }

            [JsonPropertyName("s")]
            public required string Sort { get; init; }

            [JsonPropertyName("k")]
            public required string[] SortValues { get; init; }

            [JsonPropertyName("i")]
            public required Guid LastId { get; init; }
        }
    }
}
