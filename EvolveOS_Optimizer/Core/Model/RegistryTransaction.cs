// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.Win32;

namespace EvolveOS_Optimizer.Core.Model
{
    public enum TransactionAction
    {
        ModifyValue,
        AddValue,
        DeleteValue,
        // Optional future expansions: AddKey, DeleteKey
    }

    public class RegistryTransaction
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; } = DateTime.Now;
        public TransactionAction Action { get; set; }

        public string? RootHiveName { get; set; }
        public string? SubKeyPath { get; set; }
        public string? ValueName { get; set; }

        public object? OldData { get; set; }
        public object? NewData { get; set; }
        public RegistryValueKind ValueKind { get; set; }

        public string TimeDisplay => Timestamp.ToString("HH:mm:ss");
        public string ActionDisplay => Action switch
        {
            TransactionAction.ModifyValue => $"Modified: {ValueName}",
            TransactionAction.AddValue => $"Added: {ValueName}",
            TransactionAction.DeleteValue => $"Deleted: {ValueName}",
            _ => "Unknown Action"
        };
        public string PathDisplay => $@"{RootHiveName}\{SubKeyPath}";
    }
}