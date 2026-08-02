// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace EvolveOS_Optimizer.Core.Model
{
    public enum ContextMenuTarget
    {
        Files,              // Applies to all files (*\shell)
        Folders,            // Applies to directories (Directory\shell)
        Background          // Applies to folder background/desktop (Directory\Background\shell)
    }

    public class ClassicContextMenuItem
    {
        public string Title { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;

        public string ExePath => ExecutablePath;

        public string Arguments { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public ContextMenuTarget Target { get; set; } = ContextMenuTarget.Files;

        public string KeyName { get; set; } = string.Empty;

        public string RegistryKeyName => new string(Title.Where(char.IsLetterOrDigit).ToArray());
    }

    public class ModernContextMenuItem
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("exePath")]
        public string ExePath { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public string Arguments { get; set; } = string.Empty;

        [JsonPropertyName("icon")]
        public string Icon { get; set; } = string.Empty;

        [JsonPropertyName("target")]
        public string Target { get; set; } = "All"; // e.g., "Files", "Folders", "Background", "All"
    }

    public class ModernContextMenuConfig
    {
        [JsonPropertyName("items")]
        public List<ModernContextMenuItem> Items { get; set; } = new();
    }
}