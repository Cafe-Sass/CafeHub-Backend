using System.Text.Json;

namespace CafeHub.UnitTests
{
    /// <summary>
    /// Compares two ProblemDetails JSON bodies member by member while ignoring the members that may legitimately
    /// differ between requests: the request instance URI and the correlation identifier.
    /// </summary>
    public static class ProblemDetailsAssertions
    {
        private static readonly HashSet<string> IgnoredMembers = new(StringComparer.Ordinal)
        {
            "instance",
            "correlationId",
        };

        public static void AssertEquivalent(string expectedBody, string actualBody)
        {
            using var expected = JsonDocument.Parse(expectedBody);
            using var actual = JsonDocument.Parse(actualBody);

            var expectedMembers = ReadMembers(expected.RootElement);
            var actualMembers = ReadMembers(actual.RootElement);

            Assert.Equal(
                expectedMembers.Keys.OrderBy(member => member, StringComparer.Ordinal),
                actualMembers.Keys.OrderBy(member => member, StringComparer.Ordinal));

            foreach (var member in expectedMembers)
            {
                Assert.True(actualMembers.ContainsKey(member.Key), $"The actual body is missing the '{member.Key}' member.");
                Assert.Equal(member.Value, actualMembers[member.Key]);
            }
        }

        private static Dictionary<string, string> ReadMembers(JsonElement root)
        {
            var members = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var property in root.EnumerateObject())
            {
                if (!IgnoredMembers.Contains(property.Name))
                {
                    members.Add(property.Name, property.Value.GetRawText());
                }
            }

            return members;
        }
    }
}
