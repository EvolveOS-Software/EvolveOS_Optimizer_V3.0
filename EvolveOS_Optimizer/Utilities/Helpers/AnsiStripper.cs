using System.Text.RegularExpressions;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    internal static partial class AnsiStripper
    {
        public static string StripAnsiSequences(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            var result = CsiSequenceRegex().Replace(input, string.Empty);

            result = PrivateModeRegex().Replace(result, string.Empty);

            result = OscSequenceRegex().Replace(result, string.Empty);

            result = EscSingleCharRegex().Replace(result, string.Empty);

            result = EscAnyRegex().Replace(result, string.Empty);

            return result;
        }

        [GeneratedRegex(@"\x1B\[[0-9;]*[A-Za-z]")]
        private static partial Regex CsiSequenceRegex();

        [GeneratedRegex(@"(\x1B)?\[\?[\d;]*[a-zA-Z]")]
        private static partial Regex PrivateModeRegex();

        [GeneratedRegex(@"(\x1B)?\]0;[^\x07\x1B]*(\x07|\x1B\\)?")]
        private static partial Regex OscSequenceRegex();

        [GeneratedRegex(@"\x1B[^[\]0-9]")]
        private static partial Regex EscSingleCharRegex();

        [GeneratedRegex(@"\x1B")]
        private static partial Regex EscAnyRegex();
    }
}
