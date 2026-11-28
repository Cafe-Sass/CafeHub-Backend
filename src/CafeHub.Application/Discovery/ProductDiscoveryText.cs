namespace CafeHub.Application.Discovery
{
    public static class ProductDiscoveryText
    {
        public const int MaximumLength = 200;

        public static string Normalize(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var withoutControlCharacters = new string(text.Where(character => !char.IsControl(character)).ToArray());
            return withoutControlCharacters.Trim();
        }
    }
}
