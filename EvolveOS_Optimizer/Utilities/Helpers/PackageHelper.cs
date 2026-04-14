// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Text;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class PackageHelper
    {
        public static string FormatPackageName(string id)
        {
            var d = id.IndexOf('.');
            return d >= 0 ? id[(d + 1)..].Replace('.', ' ') : id;
        }

        public static string GetPublisherDisplayName(string id)
        {
            var d = id.IndexOf('.');
            return d <= 0 ? "Unknown" : id[..d];
        }

        public static bool IsLikelyWingetPackageId(string v) =>
            !string.IsNullOrWhiteSpace(v) && !v.Contains(' ') && v.Length >= 3 && (v.Contains('.') || v.Contains('-'));

        public static string NormalizeLookupKey(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (var c in value) if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        public static IEnumerable<string> GetLookupKeys(string id, string name)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Add(string? raw)
            {
                if (string.IsNullOrWhiteSpace(raw)) return;
                keys.Add(raw);
                var n = NormalizeLookupKey(raw);
                if (!string.IsNullOrWhiteSpace(n)) keys.Add(n);
            }

            Add(id); Add(name); Add(FormatPackageName(id));
            var f = id.IndexOf('.'); if (f >= 0 && f + 1 < id.Length) Add(id[(f + 1)..]);
            var l = id.LastIndexOf('.'); if (l >= 0 && l + 1 < id.Length) Add(id[(l + 1)..]);
            return keys;
        }
    }
}