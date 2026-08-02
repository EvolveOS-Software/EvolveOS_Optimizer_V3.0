// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using EvolveOS_Optimizer.Core.Model;
using System.Runtime.InteropServices;
using System.Text;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class ContextMenuEngine
    {
        private const string Win11ClassicMenuKey = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";

        private static string GetModernConfigPath()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "EvolveOS_Optimizer", "ModernContextMenu.json");
        }

        #region Classic vs Modern Toggle

        public static bool IsClassicMenuEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey($@"{Win11ClassicMenuKey}\InprocServer32");
                return key != null;
            }
            catch
            {
                return false;
            }
        }

        public static void ToggleClassicMenu(bool enableClassic)
        {
            try
            {
                if (enableClassic)
                {
                    using var key = Registry.CurrentUser.CreateSubKey($@"{Win11ClassicMenuKey}\InprocServer32");
                    key?.SetValue("", "");
                }
                else
                {
                    Registry.CurrentUser.DeleteSubKeyTree(Win11ClassicMenuKey, false);
                }

                Debug.WriteLine($"[ContextMenuEngine] Classic menu enabled: {enableClassic}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ContextMenuEngine] Failed to toggle menu style: {ex.Message}");
            }
        }

        public static async Task RestartExplorerAsync()
        {
            try
            {
                var processes = Process.GetProcessesByName("explorer");
                foreach (var process in processes)
                {
                    process.Kill();
                }

                await Task.Delay(1000);

                if (Process.GetProcessesByName("explorer").Length == 0)
                {
                    Process.Start("explorer.exe");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ContextMenuEngine] Failed to restart explorer: {ex.Message}");
            }
        }

        #endregion

        #region Classic Menu Item Manager

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, uint cchOutBuf, IntPtr ppvReserved);

        private static string ResolveMuiString(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;

            if (!input.StartsWith("@")) return input;

            var outBuf = new StringBuilder(1024);
            int result = SHLoadIndirectString(input, outBuf, (uint)outBuf.Capacity, IntPtr.Zero);

            return result == 0 ? outBuf.ToString() : input;
        }

        private static string GetRegistryBasePath(ContextMenuTarget target)
        {
            return target switch
            {
                ContextMenuTarget.Files => @"*\shell",
                ContextMenuTarget.Folders => @"Directory\shell",
                ContextMenuTarget.Background => @"Directory\Background\shell",
                _ => @"*\shell"
            };
        }

        public static List<ClassicContextMenuItem> GetClassicItems()
        {
            var items = new List<ClassicContextMenuItem>();

            void ScanRegistryKey(string basePath, ContextMenuTarget target)
            {
                try
                {
                    using var baseKey = Registry.ClassesRoot.OpenSubKey(basePath);
                    if (baseKey == null) return;

                    foreach (var subKeyName in baseKey.GetSubKeyNames())
                    {
                        using var itemKey = baseKey.OpenSubKey(subKeyName);
                        using var commandKey = itemKey?.OpenSubKey("command");

                        if (commandKey == null) continue;

                        string rawTitle = itemKey?.GetValue("MUIVerb")?.ToString()
                                          ?? itemKey?.GetValue("")?.ToString()
                                          ?? subKeyName;

                        string title = ResolveMuiString(rawTitle);

                        if (!string.IsNullOrEmpty(title))
                        {
                            title = title.Replace("&&", "\0") // Temporarily hide double ampersands
                                         .Replace("&", "")    // Delete the accelerator symbols
                                         .Replace("\0", "&"); // Bring back the real ampersands
                        }

                        string commandStr = commandKey.GetValue("")?.ToString() ?? "";
                        string iconPath = itemKey?.GetValue("Icon")?.ToString() ?? "";

                        string exePath = commandStr;
                        string args = "";

                        if (commandStr.StartsWith("\""))
                        {
                            int endQuoteIndex = commandStr.IndexOf("\"", 1);
                            if (endQuoteIndex > 0)
                            {
                                exePath = commandStr.Substring(1, endQuoteIndex - 1);
                                args = commandStr.Substring(endQuoteIndex + 1).Trim();
                            }
                        }
                        else
                        {
                            var parts = commandStr.Split(' ', 2);
                            exePath = parts[0];
                            if (parts.Length > 1) args = parts[1];
                        }

                        items.Add(new ClassicContextMenuItem
                        {
                            Title = title,
                            ExecutablePath = exePath,
                            Arguments = args,
                            IconPath = iconPath,
                            Target = target,
                            KeyName = subKeyName
                        });
                    }
                }
                catch { }
            }

            ScanRegistryKey(@"*\shell", ContextMenuTarget.Files);
            ScanRegistryKey(@"Directory\shell", ContextMenuTarget.Folders);
            ScanRegistryKey(@"Directory\Background\shell", ContextMenuTarget.Background);

            return items;
        }

        public static void AddClassicItem(ClassicContextMenuItem item)
        {
            if (string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.ExecutablePath)) return;

            item.KeyName = new string(item.Title.Where(char.IsLetterOrDigit).ToArray());

            string basePath = GetRegistryBasePath(item.Target);
            string itemPath = $@"{basePath}\{item.KeyName}";

            try
            {
                using var itemKey = Registry.ClassesRoot.CreateSubKey(itemPath);
                if (itemKey == null) return;

                itemKey.SetValue("", item.Title);

                if (!string.IsNullOrWhiteSpace(item.IconPath))
                    itemKey.SetValue("Icon", item.IconPath);

                using var commandKey = itemKey.CreateSubKey("command");

                string args = string.IsNullOrWhiteSpace(item.Arguments) ? "" : $" {item.Arguments}";
                commandKey?.SetValue("", $"\"{item.ExecutablePath}\"{args}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ContextMenuEngine] Failed to add classic item: {ex.Message}");
            }
        }

        public static void RemoveClassicItem(ClassicContextMenuItem item)
        {
            if (string.IsNullOrEmpty(item.KeyName)) return;

            string basePath = GetRegistryBasePath(item.Target);
            try
            {
                Registry.ClassesRoot.DeleteSubKeyTree($@"{basePath}\{item.KeyName}", false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ContextMenuEngine] Failed to remove classic item: {ex.Message}");
            }
        }

        #endregion

        #region Modern Menu Item Manager (Sparse Package Bridge)

        public static async Task<bool> RegisterSparsePackageAsync(string manifestFolderPath)
        {
            try
            {
                var manifestPath = Path.Combine(manifestFolderPath, "AppxManifest.xml");
                if (!File.Exists(manifestPath))
                {
                    Debug.WriteLine("[ContextMenuEngine] AppxManifest.xml not found!");
                    return false;
                }

                var packageManager = new Windows.Management.Deployment.PackageManager();
                var manifestUri = new Uri(manifestPath);
                var externalLocationUri = new Uri(manifestFolderPath);

                var options = new Windows.Management.Deployment.AddPackageOptions
                {
                    ExternalLocationUri = externalLocationUri
                };

                var deploymentOperation = packageManager.AddPackageByUriAsync(manifestUri, options);

                var result = await deploymentOperation;

                Debug.WriteLine("[ContextMenuEngine] Sparse Package registered successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ContextMenuEngine] Failed to register Sparse Package: {ex.Message}");
                return false;
            }
        }

        public static ModernContextMenuConfig LoadModernItems()
        {
            try
            {
                string configPath = GetModernConfigPath();

                if (!File.Exists(configPath))
                {
                    return new ModernContextMenuConfig();
                }

                string json = File.ReadAllText(configPath, Encoding.UTF8);
                return JsonSerializer.Deserialize<ModernContextMenuConfig>(json) ?? new ModernContextMenuConfig();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ContextMenuEngine] Failed to load modern items: {ex.Message}");
                return new ModernContextMenuConfig();
            }
        }

        public static void SaveModernItems(List<ModernContextMenuItem> items)
        {
            try
            {
                string jsonPath = GetModernConfigPath();
                string folderPath = Path.GetDirectoryName(jsonPath)!;

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                var rootObject = new { items = items };

                string jsonContent = JsonSerializer.Serialize(rootObject,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                File.WriteAllText(jsonPath, jsonContent, Encoding.UTF8);

                Debug.WriteLine($"[ContextMenuEngine] JSON successfully written to: {jsonPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ContextMenuEngine] CRASH saving JSON: {ex.Message}");
            }
        }

        #endregion
    }
}