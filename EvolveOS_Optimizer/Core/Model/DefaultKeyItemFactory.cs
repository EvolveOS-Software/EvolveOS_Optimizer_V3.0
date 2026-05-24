// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using Vanara.PInvoke;

namespace EvolveOS_Optimizer.Core.Model
{
    public class DefaultKeyItemFactory
    {
        #region Tree Structures
        public static ObservableCollection<KeyItem> CreateNewNestedKeyTree() =>
        [
            new()
            {
                Name = "Computer",
                RootHive = HKEY.NULL,
                IsExpanded = true,
                BasePath = "",
                IsDeletable = false,
                IsRenamable = false,
                HasChildren = true,
                SelectedRootComputer = true,
                Image = "ms-appx:///Assets/PngImages/Computer.png",
                Depth = 1,
                Children =
                [
                    CreateHiveItem("HKEY_CLASSES_ROOT", HKEY.HKEY_CLASSES_ROOT),
                    CreateHiveItem("HKEY_CURRENT_USER", HKEY.HKEY_CURRENT_USER),
                    CreateHiveItem("HKEY_LOCAL_MACHINE", HKEY.HKEY_LOCAL_MACHINE),
                    CreateHiveItem("HKEY_USERS", HKEY.HKEY_USERS),
                    CreateHiveItem("HKEY_CURRENT_CONFIG", HKEY.HKEY_CURRENT_CONFIG)
                ],
            }
        ];

        public static ObservableCollection<KeyItem> CreateNewFlattenedKeyTree() =>
        [
            new()
            {
                Name = "Computer",
                RootHive = HKEY.NULL,
                IsExpanded = true,
                BasePath = "",
                IsDeletable = false,
                IsRenamable = false,
                HasChildren = true,
                SelectedRootComputer = true,
                Depth = 1,
                Image = "ms-appx:///Assets/PngImages/Computer.png",
            },
            CreateHiveItem("HKEY_CLASSES_ROOT", HKEY.HKEY_CLASSES_ROOT),
            CreateHiveItem("HKEY_CURRENT_USER", HKEY.HKEY_CURRENT_USER),
            CreateHiveItem("HKEY_LOCAL_MACHINE", HKEY.HKEY_LOCAL_MACHINE),
            CreateHiveItem("HKEY_USERS", HKEY.HKEY_USERS),
            CreateHiveItem("HKEY_CURRENT_CONFIG", HKEY.HKEY_CURRENT_CONFIG),
        ];
        #endregion

        #region Helper Methods
        private static KeyItem CreateHiveItem(string name, HKEY hive) => new()
        {
            Name = name,
            RootHive = hive,
            BasePath = "",
            IsDeletable = false,
            IsRenamable = false,
            HasChildren = true,
            Depth = 2,
            Image = "ms-appx:///Assets/PngImages/Folder.png"
        };
        #endregion
    }
}