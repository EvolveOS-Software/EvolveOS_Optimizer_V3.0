// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Utilities.Services
{
    public static class CategoryResolverService
    {
        private const string IconBrowser = "\uE774"; // Globe
        private const string IconFolder = "\uE8D5";  // Folder
        private const string IconSystem = "\uE770";  // Settings/System
        private const string IconApp = "\uE71D";     // Application list
        private const string IconMedia = "\uE8D6";   // Media/Play
        private const string IconUtility = "\uE74C"; // Wrench/Tool
        private const string IconStore = "\uE719";   // Store/Bag
        private const string IconAdvanced = "\uE9A1"; // Code/Advanced

        private static readonly IReadOnlyDictionary<int, CategoryInfo> Categories =
            new Dictionary<int, CategoryInfo>
            {
                // Core System & Windows Features
                [3001] = new("Internet Explorer", 10, IconBrowser),
                [3002] = new("Windows Explorer", 20, IconFolder),
                [3003] = new("System", 30, IconSystem),
                [3004] = new("Advanced", 40, IconAdvanced),
                [3025] = new("Windows", 50, IconSystem),
                [3031] = new("Microsoft Store", 60, IconStore),

                // Web Browsers
                [3005] = new("Microsoft Edge (Legacy)", 70, IconBrowser),
                [3006] = new("Microsoft Edge", 80, IconBrowser),
                [3026] = new("Mozilla Firefox", 90, IconBrowser),
                [3027] = new("Opera", 100, IconBrowser),
                [3028] = new("Apple Safari", 110, IconBrowser),
                [3029] = new("Google Chrome", 120, IconBrowser),
                [3032] = new("CCleaner Browser", 130, IconBrowser),
                [3033] = new("Vivaldi", 140, IconBrowser),
                [3034] = new("Brave", 150, IconBrowser),
                [3035] = new("Opera GX", 160, IconBrowser),
                [3037] = new("Avast Secure Browser", 170, IconBrowser),
                [3038] = new("AVG Secure Browser", 180, IconBrowser),
                [3039] = new("Arc Browser", 190, IconBrowser),
                [3043] = new("Norton Private Browser", 200, IconBrowser),
                [3044] = new("Avira Secure Browser", 210, IconBrowser),

                // Software Categories
                [3021] = new("Applications", 220, IconApp),
                [3022] = new("Internet", 230, IconBrowser),
                [3023] = new("Multimedia", 240, IconMedia),
                [3024] = new("Utilities", 250, IconUtility),

                // Communications & Specific Apps
                [3030] = new("Mozilla Thunderbird", 260, "\uE715"),
                [3036] = new("Spotify", 270, IconMedia),
                [3040] = new("iTunes", 280, IconMedia),
                [3042] = new("WhatsApp", 290, "\uE717")
            };

        public static CategoryInfo TryMapLangSecRef(CleanerEntry entry)
        {
            if (entry.LangSecRef is int code && Categories.TryGetValue(code, out var category))
                return category;

            if (!string.IsNullOrWhiteSpace(entry.Section))
                return new CategoryInfo(entry.Section, 1000, IconApp);

            return new CategoryInfo("Other Applications", 2000, IconApp);
        }

        public readonly record struct CategoryInfo(string Name, int Order, string Glyph);
    }
}