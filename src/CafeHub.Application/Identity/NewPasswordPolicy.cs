using System.Text;

namespace CafeHub.Application.Identity
{
    public static class NewPasswordPolicy
    {
        public const string Requirements = "Use 12–128 characters with an uppercase letter, a lowercase letter, a digit, and a punctuation character or symbol.";

        public static bool IsValid(string? password)
        {
            if (password is null || password.Length is < 12 or > 128)
            {
                return false;
            }

            var upper = false;
            var lower = false;
            var digit = false;
            var special = false;
            foreach (var character in password.EnumerateRunes())
            {
                upper |= Rune.IsUpper(character);
                lower |= Rune.IsLower(character);
                digit |= Rune.IsDigit(character);
                special |= Rune.IsPunctuation(character) || Rune.IsSymbol(character);
            }

            return upper && lower && digit && special;
        }
    }
}
