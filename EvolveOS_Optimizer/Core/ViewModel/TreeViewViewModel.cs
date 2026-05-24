// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Text;
using EvolveOS_Optimizer.Core.Model;
using Microsoft.Win32.SafeHandles;
using Vanara.PInvoke;
using static Vanara.PInvoke.AdvApi32;

namespace EvolveOS_Optimizer.Core.ViewModel;

public class TreeViewViewModel
{
    public TreeViewViewModel()
    {
        KeyItems = DefaultKeyItemFactory.CreateNewNestedKeyTree();
        FlatKeyItems = DefaultKeyItemFactory.CreateNewFlattenedKeyTree();

        LastRenamedNewName = string.Empty;
    }

    #region Fields and Properties
    public ObservableCollection<KeyItem> KeyItems { get; }
    public ObservableCollection<KeyItem> FlatKeyItems { get; }

    public string LastRenamedNewName { get; set; }
    public bool CreatingNewKey { get; set; }
    #endregion

    #region Methods

    public async Task ExpandChildrenAsync(KeyItem item)
    {
        var flattenedKeyItemNodeTree = GetFlattenNodes(KeyItems);
        var itemFromFlattenedTreeItem = flattenedKeyItemNodeTree.FirstOrDefault(x => x.PathForPwsh == item.PathForPwsh);

        if (itemFromFlattenedTreeItem == null) return;

        if (item.Depth != 1 && !itemFromFlattenedTreeItem.IsLoaded)
        {
            var newChildren = await Task.Run(() =>
                EnumerateRegistryKeys(itemFromFlattenedTreeItem.RootHive, itemFromFlattenedTreeItem.Path, itemFromFlattenedTreeItem).ToList()
            );

            App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
            {
                itemFromFlattenedTreeItem.Children.Clear();
                foreach (var child in newChildren)
                {
                    itemFromFlattenedTreeItem.Children.Add(child);
                }
                itemFromFlattenedTreeItem.IsLoaded = true;

                InsertChildren(item, itemFromFlattenedTreeItem);
            });
        }
        else
        {
            InsertChildren(item, itemFromFlattenedTreeItem);
        }
    }

    private void InsertChildren(KeyItem targetItem, KeyItem sourceNode)
    {
        targetItem.IsExpanded = true;
        sourceNode.IsExpanded = true;

        int index = FlatKeyItems.IndexOf(targetItem);

        if (index == -1) return;

        index++;
        foreach (var child in sourceNode.Children)
        {
            FlatKeyItems.Insert(index, child);
            index++;
        }
    }

    public void CollapseChildren(KeyItem item)
    {
        item.IsExpanded = false;

        var flattenedKeyItemNodeTree = GetFlattenNodes(KeyItems);
        var itemFromFlattenedTree = flattenedKeyItemNodeTree.FirstOrDefault(x => x.PathForPwsh == item.PathForPwsh);

        if (itemFromFlattenedTree != null)
        {
            itemFromFlattenedTree.IsExpanded = false;
        }

        RemoveAll(item);
    }

    private IEnumerable<KeyItem> EnumerateRegistryKeys(HKEY hRootKey, string? subRoot, KeyItem parent)
    {
        string path = subRoot ?? string.Empty;
        var keys = new List<KeyItem>();

        var result = RegOpenKeyEx(hRootKey, path, 0, REGSAM.KEY_READ, out SafeRegistryHandle phKey);

        if (result.Failed) return Enumerable.Empty<KeyItem>();

        using (phKey)
        {
            result = RegQueryInfoKey(phKey, null, ref Unsafe.NullRef<uint>(), default,
                out uint cSubKeys, out uint cbMaxSubKeyLen, out _, out _, out _, out _, out _, out _);

            if (result.Failed) return Enumerable.Empty<KeyItem>();

            StringBuilder szName;
            uint cchName;

            for (uint dwIndex = 0; dwIndex < cSubKeys; dwIndex++)
            {
                cchName = cbMaxSubKeyLen + 1;
                szName = new((int)cchName, (int)cchName);

                System.Runtime.InteropServices.ComTypes.FILETIME ftLastWrite;

                result = RegEnumKeyEx(phKey, dwIndex, szName, ref cchName, default, null, ref Unsafe.NullRef<uint>(), out ftLastWrite);
                if (result.Failed) continue;

                string childName = szName.ToString();
                string childPath = string.IsNullOrEmpty(path) ? childName : $"{path}\\{childName}";

                uint childSubKeys = 0;
                uint childValues = 0;

                if (RegOpenKeyEx(hRootKey, childPath, 0, REGSAM.KEY_READ, out SafeRegistryHandle hChild).Succeeded)
                {
                    using (hChild)
                    {
                        RegQueryInfoKey(hChild, null, ref Unsafe.NullRef<uint>(), default,
                            out childSubKeys, out _, out _, out childValues, out _, out _, out _, out _);
                    }
                }

                long ticks = (((long)ftLastWrite.dwHighDateTime) << 32) + (uint)ftLastWrite.dwLowDateTime;
                DateTime lastWriteDate = DateTime.FromFileTime(ticks);

                keys.Add(new()
                {
                    Name = childName,
                    RootHive = hRootKey,
                    BasePath = path,
                    IsDeletable = true,
                    IsRenamable = true,
                    HasChildren = childSubKeys > 0,
                    Image = "ms-appx:///Assets/PngImages/Folder.png",
                    Depth = parent.Depth + 1,
                    Parent = parent,
                    CreatedAt = lastWriteDate,
                    SubKeysCount = (int)childSubKeys,
                    ValuesCount = (int)childValues
                });
            }
        }

        return keys;
    }

    private Win32Error HasSubKeys(HKEY hRootKey, string subRoot, out bool hasChildren)
    {
        hasChildren = false;

        Win32Error result = RegOpenKeyEx(hRootKey, subRoot, 0, REGSAM.KEY_READ, out SafeRegistryHandle phKey);
        if (result.Failed)
        {
            return result;
        }

        using (phKey)
        {
            result = RegQueryInfoKey(phKey, null, ref Unsafe.NullRef<uint>(), default,
                out uint cSubKeys, out _, out _, out _, out _, out _, out _, out _);

            if (result.Succeeded)
            {
                hasChildren = (cSubKeys > 0);
            }
        }

        return result;
    }

    private void RemoveAll(KeyItem parentItem)
    {
        int startIndex = FlatKeyItems.IndexOf(parentItem);
        if (startIndex < 0 || startIndex >= FlatKeyItems.Count - 1) return;

        int depth = parentItem.Depth;
        int itemsToRemove = 0;
        int checkIndex = startIndex + 1;

        while (checkIndex < FlatKeyItems.Count)
        {
            if (FlatKeyItems[checkIndex].Depth > depth)
            {
                itemsToRemove++;
                checkIndex++;
            }
            else
            {
                break;
            }
        }

        for (int i = 0; i < itemsToRemove; i++)
        {
            FlatKeyItems.RemoveAt(startIndex + 1);
        }
    }

    private IEnumerable<KeyItem> GetFlattenNodes(IEnumerable<KeyItem> masterList)
    {
        foreach (var node in masterList)
        {
            yield return node;

            foreach (var children in GetFlattenNodes(node.Children))
            {
                yield return children;
            }
        }
    }

    public Win32Error DeleteRegistryKey(KeyItem item)
    {
        return RegDeleteTree(item.RootHive, item.Path);
    }

    public Win32Error RenameRegistryKey(KeyItem item, string newName)
    {
        if (item.Parent == null) return Win32Error.ERROR_ACCESS_DENIED;

        var result = RegOpenKeyEx(item.RootHive, item.BasePath, 0, REGSAM.KEY_WRITE, out var hParentKey);
        if (result.Failed) return result;

        result = RegRenameKey(hParentKey, item.Name, newName);

        hParentKey?.Dispose();

        return result;
    }
    #endregion
}