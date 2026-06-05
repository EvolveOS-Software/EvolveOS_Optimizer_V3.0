// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Documents;

namespace EvolveOS_Optimizer.Utilities.Helpers;

internal static class TerminalLineRenderer
{
    #region Brushes & Styling Constants
    public static readonly SolidColorBrush DefaultBrush =
        new(Color.FromArgb(0xFF, 0xCC, 0xCC, 0xCC));

    public static readonly SolidColorBrush ErrorBrush =
        new(Color.FromArgb(0xFF, 0xF4, 0x4C, 0x4C));

    public static readonly SolidColorBrush WarningBrush =
        new(Color.FromArgb(0xFF, 0xFF, 0xCC, 0x00));

    public static readonly SolidColorBrush SuccessBrush =
        new(Color.FromArgb(0xFF, 0x6A, 0xBF, 0x69));

    public static readonly SolidColorBrush MetadataBrush =
        new(Color.FromArgb(0xFF, 0x56, 0x9C, 0xD6));

    public static readonly SolidColorBrush SeparatorBrush =
        new(Color.FromArgb(0xFF, 0x60, 0x60, 0x60));

    public static readonly SolidColorBrush BarTrackBrush =
        new(Color.FromArgb(0xFF, 0x40, 0x40, 0x40));

    public static readonly Windows.UI.Color TerminalBackground =
        Color.FromArgb(0xFF, 0x1E, 0x1E, 0x1E);

    public static readonly FontFamily MonoFont = new("Consolas");
    #endregion

    #region Color & Styling Logic
    public static SolidColorBrush GetLineBrush(string line)
    {
        var trimmed = line.TrimStart();

        if (trimmed.Length > 2 && trimmed[0] == '[')
        {
            var closeBracket = trimmed.IndexOf("] ", StringComparison.Ordinal);
            if (closeBracket > 0 && closeBracket < 30)
                trimmed = trimmed.Substring(closeBracket + 2).TrimStart();
        }

        if (trimmed.StartsWith("Command:", StringComparison.Ordinal)
            || trimmed.StartsWith("Start Time:", StringComparison.Ordinal)
            || trimmed.StartsWith("End Time:", StringComparison.Ordinal)
            || trimmed.StartsWith("Process return value:", StringComparison.Ordinal))
            return MetadataBrush;

        if (trimmed == "---")
            return SeparatorBrush;

        if (line.Contains("error", StringComparison.OrdinalIgnoreCase)
            || line.Contains("failed", StringComparison.OrdinalIgnoreCase))
            return ErrorBrush;

        if (line.Contains("warning", StringComparison.OrdinalIgnoreCase))
            return WarningBrush;

        if (line.Contains("successfully", StringComparison.OrdinalIgnoreCase)
            || line.Contains("complete", StringComparison.OrdinalIgnoreCase))
            return SuccessBrush;

        return DefaultBrush;
    }
    #endregion

    #region XAML Rendering Generation
    public static Run[] CreateLineRuns(string line, bool appendNewline = true)
    {
        var nl = appendNewline ? "\x0a" : "";

        bool hasFilledBlocks = false;
        foreach (char c in line)
        {
            if (c >= '\u2588' && c <= '\u258F') { hasFilledBlocks = true; break; }
        }
        if (hasFilledBlocks && line.Contains('\u2591'))
        {
            var runs = new List<Run>();
            int i = 0;
            while (i < line.Length)
            {
                bool isTrack = line[i] == '\u2591';
                int start = i;
                while (i < line.Length && (line[i] == '\u2591') == isTrack)
                    i++;

                var segment = line.Substring(start, i - start);
                if (isTrack)
                {
                    runs.Add(new Run
                    {
                        Text = segment.Replace('\u2591', '\u2588'),
                        Foreground = BarTrackBrush
                    });
                }
                else
                {
                    bool isLastSegment = i >= line.Length;
                    runs.Add(new Run
                    {
                        Text = isLastSegment ? segment + nl : segment,
                        Foreground = GetLineBrush(line)
                    });
                }
            }
            if (appendNewline && runs.Count > 0 && !runs[^1].Text.EndsWith("\x0a"))
                runs[^1].Text += "\x0a";
            return runs.ToArray();
        }

        return [new Run
        {
            Text = line + nl,
            Foreground = GetLineBrush(line)
        }];
    }
    #endregion

    #region Text Analysis Utilities
    public static bool LooksLikeProgressBar(string line)
    {
        foreach (char c in line)
        {
            if (c >= '\u2588' && c <= '\u258F') return true;
            if (c == '\u2591') return true;
        }
        return false;
    }
    #endregion
}
