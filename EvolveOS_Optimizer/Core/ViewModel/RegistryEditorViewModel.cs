// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Extensions;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public partial class RegistryEditorViewModel : ObservableObject
    {
        private CancellationTokenSource? _cancellationTokenSource = new();

        public ObservableCollection<KeyItem> KeyItems { get; } = new();
        public ObservableCollection<KeyItem> FlatKeyItems { get; } = new();

        public RegistryEditorViewModel()
        {
        }

        public async Task DeleteSelectedKey(KeyItem key)
        {
            if (key == null)
                return;

            string command = $"Remove-Item -Path '{key.PathForPwsh}' -Recurse -Force";
            await CommandExecutor.RunCommandAsTrustedInstaller(command, isPowerShell: true);

            if (key.Parent != null && key.Parent.Children != null)
            {
                key.Parent.Children.Remove(key);

                if (key.Parent.Children.Count == 0)
                {
                    key.Parent.HasChildren = false;
                }
            }
        }

        public async Task RenameSelectedKey(KeyItem key, string renamingKey)
        {
            key.IsRenaming = false;
            var previousPath = key.Path;

            var pathItems = key.PathForPwsh.Split('\\').ToList();
            pathItems.RemoveAt(pathItems.Count - 1);
            var parentPath = string.Join('\\', pathItems);

            string command = $"if (!(Test-Path '{key.PathForPwsh}')) {{ New-Item -Path '{parentPath}' -Name '{key.Name}' -Force }} else {{ Rename-Item -Path '{key.PathForPwsh}' -NewName '{renamingKey}' }}";

            await CommandExecutor.RunCommandAsTrustedInstaller(command, isPowerShell: true);

            key.Name = renamingKey;

            var flattenedItems = key.Children.GetFlattenNodes();
            foreach (var item in flattenedItems)
            {
                item.BasePath = item.BasePath.Replace(previousPath, key.Path);
            }
        }

        public async Task ExportSelectedKeyTree(KeyItem key)
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            picker.FileTypeChoices.Add("Registration Entries", new System.Collections.Generic.List<string> { ".reg" });

            IntPtr hwnd = Win32Helper.GetActiveWindow();

            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();

            if (file != null)
            {
                string command = $"REG EXPORT \"{key.PathForCmd}\" \"{file.Path}\" /y";
                await CommandExecutor.RunCommand(command, isPowerShell: false);
            }
        }

        public void Cleanup()
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }

            Debug.WriteLine($"[{this.GetType().Name}] Cleanup complete. References broken.");
        }
    }
}