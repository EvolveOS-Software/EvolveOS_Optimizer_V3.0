namespace EvolveOS_Optimizer.Core
{
    public class Enums
    {
        public static class Dialog
        {
            public enum Button
            {
                None,
                Yes,
                No
            }
        }

        public static class Log
        {
            [Flags]
            public enum Levels
            {
                Debug = 1,
                Information = 2,
                Warning = 4,
                Error = 8
            }
        }

        public enum ServiceState : uint
        {
            Stopped = 0x00000001,
            StartPending = 0x00000002,
            StopPending = 0x00000003,
            Running = 0x00000004,
            ContinuePending = 0x00000005,
            PausePending = 0x00000006,
            Paused = 0x00000007
        }

        internal enum ServiceControlAction
        {
            Start,
            Stop,
            Restart
        }

        public static class Memory
        {
            [Flags]
            public enum Areas
            {
                None = 0,
                CombinedPageList = 1,
                ModifiedFileCache = 2,
                ModifiedPageList = 4,
                RegistryCache = 8,
                StandbyList = 16,
                StandbyListLowPriority = 32,
                SystemFileCache = 64,
                WorkingSet = 128,
                DiskCleanup = 256,
                WindowsOld = 512,
                FlushDns = 1024
            }

            public static class Optimization
            {
                public enum Reason
                {
                    LowMemory,
                    Manual,
                    Schedule
                }
            }

            public enum Unit { B, KB, MB, GB, TB, PB, EB, ZB, YB }
        }

        public enum Priority
        {
            Low,
            Normal,
            High
        }

        public enum DNSSettingPreference
        {
            Recommended,
            Privacy,
        }

        public enum MessageWindowState
        {
            Warning,
            NotSupported,
            AlreadyRunning
        }

        public enum StartupSourceType
        {
            RegistryHKCU,
            RegistryHKLM,
            FolderUser,
            FolderCommon
        }

        public enum ExcludeType
        {
            File,
            Path,
            Reg
        }

        public enum FileKeyFlag
        {
            None,
            Recurse,
            RemoveSelf
        }

        public enum AiProvider
        {
            Groq,
            Gemini,
            OpenRouter,
            Cohere,
            Mistral
        }
    }
}
