// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using EvolveOS_Optimizer.Core.Model;
using Microsoft.Win32;

namespace EvolveOS_Optimizer.Utilities.Managers
{
    public static class RegistryTransactionManager
    {
        #region Collections

        public static ObservableCollection<RegistryTransaction> SessionHistory { get; } = new();
        #endregion

        #region Transaction Logic
        public static void RecordTransaction(RegistryTransaction transaction)
        {
            App.UIThreadDispatcher?.TryEnqueue(() =>
            {
                SessionHistory.Insert(0, transaction);
            });
        }

        public static void UndoTransaction(RegistryTransaction transaction)
        {
            try
            {
                if (string.IsNullOrEmpty(transaction.RootHiveName)) return;

                RegistryKey? rootKey = GetRootKey(transaction.RootHiveName);
                if (rootKey == null) return;

                string safeSubKeyPath = transaction.SubKeyPath ?? string.Empty;
                string safeValueName = transaction.ValueName ?? string.Empty;

                using (RegistryKey? key = rootKey.OpenSubKey(safeSubKeyPath, writable: true))
                {
                    if (key != null)
                    {
                        switch (transaction.Action)
                        {
                            case TransactionAction.ModifyValue:
                            case TransactionAction.AddValue:
                                if (transaction.OldData == null)
                                {
                                    key.DeleteValue(safeValueName, throwOnMissingValue: false);
                                }
                                else
                                {
                                    key.SetValue(safeValueName, transaction.OldData, transaction.ValueKind);
                                }
                                break;

                            case TransactionAction.DeleteValue:
                                if (transaction.OldData != null)
                                {
                                    key.SetValue(safeValueName, transaction.OldData, transaction.ValueKind);
                                }
                                break;
                        }
                    }
                }

                App.UIThreadDispatcher?.TryEnqueue(() =>
                {
                    SessionHistory.Remove(transaction);
                });

                Debug.WriteLine($"[Registry Undo] Successfully reverted {safeValueName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Registry Undo Error] {ex.Message}");
            }
        }

        public static void UndoLast()
        {
            var lastChange = SessionHistory.FirstOrDefault();
            if (lastChange != null)
            {
                UndoTransaction(lastChange);
            }
        }

        #endregion

        #region Helpers

        private static RegistryKey? GetRootKey(string rootName)
        {
            return rootName switch
            {
                "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
                "HKEY_CURRENT_USER" => Registry.CurrentUser,
                "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
                "HKEY_USERS" => Registry.Users,
                "HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
                _ => null
            };
        }

        #endregion
    }
}