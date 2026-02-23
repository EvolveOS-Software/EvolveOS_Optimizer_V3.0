using System.Net.NetworkInformation;
using EvolveOS_Optimizer.Utilities.Controls;

namespace EvolveOS_Optimizer.Utilities.Managers
{
    public class DnsManager
    {
        private const string PRIMARY_INTERFACE_NAME = "Ethernet";

        private bool ExecuteNetshCommand(string command)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = command,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        Verb = "runas"
                    }
                };
                process.Start();
                process.WaitForExit(10000);

                if (process.ExitCode != 0)
                {
                    string error = process.StandardError.ReadToEnd();

                    Debug.WriteLine($"Netsh command failed: {command}. Error: {error}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug($"Exception during netsh execution: {ex.Message}");
                return false;
            }
        }

        public bool SetIpv4Dns(string primary, string secondary)
        {
            string? interfaceName = GetActiveInterface()?.Name;

            if (string.IsNullOrWhiteSpace(interfaceName))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(primary) || primary == "0.0.0.0")
            {
                return ExecuteNetshCommand($"interface ipv4 set dnsservers name=\"{interfaceName}\" source=dhcp");
            }
            else
            {
                bool primarySuccess = ExecuteNetshCommand($"interface ipv4 set dnsservers name=\"{interfaceName}\" static {primary} primary");

                if (primarySuccess)
                {
                    if (!string.IsNullOrWhiteSpace(secondary) && secondary != "0.0.0.0")
                    {
                        return ExecuteNetshCommand($"interface ipv4 add dnsservers name=\"{interfaceName}\" {secondary} index=2");
                    }
                    return true;
                }
                return false;
            }
        }

        public bool SetIpv6Dns(string primary, string secondary)
        {
            string? interfaceName = GetActiveInterface()?.Name;

            if (string.IsNullOrWhiteSpace(interfaceName))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(primary) || primary == "::")
            {
                return ExecuteNetshCommand($"interface ipv6 set dnsservers name=\"{interfaceName}\" source=dhcp");
            }
            else
            {
                bool primarySuccess = ExecuteNetshCommand($"interface ipv6 set dnsservers name=\"{interfaceName}\" static {primary} primary");

                if (primarySuccess)
                {
                    if (!string.IsNullOrWhiteSpace(secondary) && secondary != "::")
                    {
                        return ExecuteNetshCommand($"interface ipv6 add dnsservers name=\"{interfaceName}\" {secondary} index=2");
                    }
                    return true;
                }
                return false;
            }
        }



        private NetworkInterface? GetActiveInterface()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up &&
                                     (n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                                      n.NetworkInterfaceType == NetworkInterfaceType.Ethernet) &&
                                     n.GetIPProperties().GatewayAddresses.Count > 0);
        }

        private string GetDnsAddress(bool isIpv4, int index)
        {
            var activeInterface = GetActiveInterface();
            if (activeInterface == null)
            {
                return isIpv4 ? "0.0.0.0" : "::";
            }

            var dnsServers = activeInterface.GetIPProperties().DnsAddresses;

            var targetDns = dnsServers.Where(addr => isIpv4 ? addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork : addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                                      .Skip(index - 1)
                                      .FirstOrDefault();

            if (targetDns != null)
            {
                return targetDns.ToString();
            }

            return isIpv4 ? "0.0.0.0" : "::";
        }

        public string GetCurrentIpv4Primary() => GetDnsAddress(true, 1);
        public string GetCurrentIpv4Secondary() => GetDnsAddress(true, 2);
        public string GetCurrentIpv6Primary() => GetDnsAddress(false, 1);
        public string GetCurrentIpv6Secondary() => GetDnsAddress(false, 2);
    }
}
