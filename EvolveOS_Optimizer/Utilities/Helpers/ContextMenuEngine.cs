// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using EvolveOS_Optimizer.Core.Model;
using Microsoft.Win32;

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

        private static string[] GetRegistryBasePaths(ClassicContextMenuItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.SpecificExtension) && item.SpecificExtension != "*")
            {
                return item.SpecificExtension.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(ext => ext.Trim())
                           .Select(ext => ext.StartsWith(".") ? ext : "." + ext)
                           .Select(ext => $@"{ext}\shell")
                           .ToArray();
            }

            string defaultPath = item.Target switch
            {
                ContextMenuTarget.Files => @"*\shell",
                ContextMenuTarget.Folders => @"Directory\shell",
                ContextMenuTarget.Background => @"Directory\Background\shell",
                _ => @"*\shell"
            };

            return new[] { defaultPath };
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

                        string iconPath = itemKey?.GetValue("Icon")?.ToString() ?? "";

                        bool isExtended = itemKey?.GetValue("Extended") != null;
                        string position = itemKey?.GetValue("Position")?.ToString() ?? "Default";

                        bool isSubMenu = itemKey?.OpenSubKey("shell") != null || itemKey?.GetValue("SubCommands") != null;

                        string exePath = "";
                        string args = "";

                        if (commandKey != null)
                        {
                            string commandStr = commandKey.GetValue("")?.ToString() ?? "";

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
                        }

                        var classicItem = new ClassicContextMenuItem
                        {
                            Title = title,
                            ExecutablePath = exePath,
                            Arguments = args,
                            IconPath = iconPath,
                            Target = target,
                            KeyName = subKeyName,
                            Extended = isExtended,
                            Position = position,
                            SpecificExtension = target == ContextMenuTarget.Files ? "*" : "",
                            IsSubMenu = isSubMenu
                        };

                        if (isSubMenu && itemKey != null)
                        {
                            using var shellKey = itemKey.OpenSubKey("shell");
                            if (shellKey != null)
                            {
                                foreach (var childSubKeyName in shellKey.GetSubKeyNames())
                                {
                                    using var childItemKey = shellKey.OpenSubKey(childSubKeyName);
                                    using var childCommandKey = childItemKey?.OpenSubKey("command");

                                    if (childCommandKey == null) continue;

                                    string childTitle = ResolveMuiString(childItemKey?.GetValue("")?.ToString() ?? childSubKeyName);
                                    string childCommandStr = childCommandKey.GetValue("")?.ToString() ?? "";
                                    string childIcon = childItemKey?.GetValue("Icon")?.ToString() ?? "";

                                    string childExe = childCommandStr;
                                    string childArgs = "";
                                    if (childCommandStr.StartsWith("\""))
                                    {
                                        int endQ = childCommandStr.IndexOf("\"", 1);
                                        if (endQ > 0)
                                        {
                                            childExe = childCommandStr.Substring(1, endQ - 1);
                                            childArgs = childCommandStr.Substring(endQ + 1).Trim();
                                        }
                                    }

                                    classicItem.SubItems.Add(new ClassicContextMenuItem
                                    {
                                        Title = childTitle,
                                        ExecutablePath = childExe,
                                        Arguments = childArgs,
                                        IconPath = childIcon,
                                        Target = target,
                                        KeyName = childSubKeyName
                                    });
                                }
                            }
                        }

                        items.Add(classicItem);
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
            if (string.IsNullOrWhiteSpace(item.Title)) return;

            if (!item.IsSubMenu && string.IsNullOrWhiteSpace(item.ExecutablePath)) return;

            item.KeyName = new string(item.Title.Where(char.IsLetterOrDigit).ToArray());

            var basePaths = GetRegistryBasePaths(item);

            foreach (var basePath in basePaths)
            {
                string itemPath = $@"{basePath}\{item.KeyName}";

                try
                {
                    using var itemKey = Registry.ClassesRoot.CreateSubKey(itemPath);
                    if (itemKey == null) continue;

                    itemKey.SetValue("", item.Title);

                    if (!string.IsNullOrWhiteSpace(item.IconPath))
                        itemKey.SetValue("Icon", item.IconPath);

                    if (item.Extended)
                        itemKey.SetValue("Extended", string.Empty, RegistryValueKind.String);

                    if (!string.IsNullOrWhiteSpace(item.Position) && item.Position != "Default")
                        itemKey.SetValue("Position", item.Position, RegistryValueKind.String);

                    if (item.IsSubMenu)
                    {
                        itemKey.SetValue("SubCommands", string.Empty, RegistryValueKind.String);

                        using var shellKey = itemKey.CreateSubKey("shell");
                        if (shellKey != null)
                        {
                            foreach (var subItem in item.SubItems)
                            {
                                string subKeyName = new string(subItem.Title.Where(char.IsLetterOrDigit).ToArray());
                                using var subItemKey = shellKey.CreateSubKey(subKeyName);
                                if (subItemKey != null)
                                {
                                    subItemKey.SetValue("", subItem.Title);
                                    if (!string.IsNullOrWhiteSpace(subItem.IconPath))
                                        subItemKey.SetValue("Icon", subItem.IconPath);

                                    using var subCmdKey = subItemKey.CreateSubKey("command");
                                    string args = string.IsNullOrWhiteSpace(subItem.Arguments) ? "" : $" {subItem.Arguments}";
                                    subCmdKey?.SetValue("", $"\"{subItem.ExecutablePath}\"{args}");
                                }
                            }
                        }
                    }
                    else
                    {
                        using var commandKey = itemKey.CreateSubKey("command");
                        string args = string.IsNullOrWhiteSpace(item.Arguments) ? "" : $" {item.Arguments}";
                        commandKey?.SetValue("", $"\"{item.ExecutablePath}\"{args}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ContextMenuEngine] Failed to add classic item to {itemPath}: {ex.Message}");
                }
            }
        }

        public static void RemoveClassicItem(ClassicContextMenuItem item)
        {
            if (string.IsNullOrEmpty(item.KeyName)) return;

            var basePaths = GetRegistryBasePaths(item);

            foreach (var basePath in basePaths)
            {
                try
                {
                    Registry.ClassesRoot.DeleteSubKeyTree($@"{basePath}\{item.KeyName}", false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ContextMenuEngine] Failed to remove classic item from {basePath}: {ex.Message}");
                }
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

                try
                {
                    string assetsFolder = Path.Combine(manifestFolderPath, "Assets");
                    if (!Directory.Exists(assetsFolder))
                    {
                        Directory.CreateDirectory(assetsFolder);
                    }

                    string logoDestination = Path.Combine(assetsFolder, "EvolveOS_Optimizer-Logo.png");

                    if (!File.Exists(logoDestination))
                    {
                        string localLogo1 = Path.Combine(AppContext.BaseDirectory, "logo.png");
                        string localLogo2 = Path.Combine(AppContext.BaseDirectory, "EvolveOS_Optimizer-Logo.png");

                        if (File.Exists(localLogo2))
                        {
                            File.Copy(localLogo2, logoDestination, true);
                        }
                        else if (File.Exists(localLogo1))
                        {
                            File.Copy(localLogo1, logoDestination, true);
                        }
                        else
                        {
                            var assembly = Assembly.GetExecutingAssembly();

                            string? resourceName = assembly.GetManifestResourceNames()
                                .FirstOrDefault(n => n.EndsWith("EvolveOS_Optimizer-Logo.png", StringComparison.OrdinalIgnoreCase)
                                                  || n.EndsWith("logo.png", StringComparison.OrdinalIgnoreCase));

                            if (!string.IsNullOrEmpty(resourceName))
                            {
                                using Stream? stream = assembly.GetManifestResourceStream(resourceName);
                                if (stream != null)
                                {
                                    using FileStream fs = new FileStream(logoDestination, FileMode.Create, FileAccess.Write);
                                    stream.CopyTo(fs);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ContextMenuEngine] Failed to prepare Assets folder: {ex.Message}");
                }

                var packageManager = new Windows.Management.Deployment.PackageManager();
                var manifestUri = new Uri(manifestPath);
                var externalLocationUri = new Uri(manifestFolderPath);

                var options = new Windows.Management.Deployment.AddPackageOptions
                {
                    ExternalLocationUri = externalLocationUri,
                    DeferRegistrationWhenPackagesAreInUse = true
                };

                var deploymentTask = packageManager.AddPackageByUriAsync(manifestUri, options).AsTask();
                var completedTask = await Task.WhenAny(deploymentTask, Task.Delay(5000));

                if (completedTask == deploymentTask)
                {
                    Debug.WriteLine("[ContextMenuEngine] Sparse Package registered successfully.");
                }
                else
                {
                    Debug.WriteLine("[ContextMenuEngine] Sparse Package registration deferred or timed out.");
                }

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