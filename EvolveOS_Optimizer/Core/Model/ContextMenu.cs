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

        public bool Extended { get; set; } = false;
        public string SpecificExtension { get; set; } = string.Empty;
        public string Position { get; set; } = "Default";

        public bool IsSubMenu { get; set; } = false;
        public List<ClassicContextMenuItem> SubItems { get; set; } = new();
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

        [JsonPropertyName("extended")]
        public bool Extended { get; set; } = false;

        [JsonPropertyName("isSeparator")]
        public bool IsSeparator { get; set; } = false;

        [JsonPropertyName("specificExtension")]
        public string SpecificExtension { get; set; } = string.Empty;

        [JsonPropertyName("position")]
        public string Position { get; set; } = "Default";

        [JsonPropertyName("isSubMenu")]
        public bool IsSubMenu { get; set; } = false;

        [JsonPropertyName("subItems")]
        public List<ModernContextMenuItem> SubItems { get; set; } = new();
    }

    public class ModernContextMenuConfig
    {
        [JsonPropertyName("items")]
        public List<ModernContextMenuItem> Items { get; set; } = new();
    }
}