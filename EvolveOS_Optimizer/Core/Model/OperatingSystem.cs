namespace EvolveOS_Optimizer.Core.Model
{
    public class OperatingSystem
    {
        public bool HasCombinedPageList { get { return IsWindows8OrGreater; } }

        public bool HasHotkeyManager { get { return IsWindowsVistaOrGreater; } }

        public bool HasModifiedFileCache { get { return IsWindowsXpOrGreater; } }

        public bool HasModifiedPageList { get { return IsWindowsVistaOrGreater; } }

        public bool HasRegistryHive { get { return IsWindows81OrGreater; } }

        public bool HasStandbyList { get { return IsWindowsVistaOrGreater; } }

        public bool HasSystemFileCache { get { return IsWindowsXpOrGreater; } }

        public bool HasWorkingSet { get { return IsWindowsXpOrGreater; } }

        public bool Is64Bit { get; set; }

        public bool IsWindows7OrGreater { get; set; }

        public bool IsWindows81OrGreater { get; set; }

        public bool IsWindows8OrGreater { get; set; }

        public bool IsWindowsVistaOrGreater { get; set; }

        public bool IsWindowsXpOrGreater { get; set; }
    }
}
