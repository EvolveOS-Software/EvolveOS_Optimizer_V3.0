namespace EvolveOS_Optimizer.Core.Model
{
    public class DnsPreset
    {
        public string? Name { get; set; }
        public string? Ipv4Primary { get; set; }
        public string? Ipv4Secondary { get; set; }
        public string? Ipv6Primary { get; set; }
        public string? Ipv6Secondary { get; set; }

        public static readonly IReadOnlyList<DnsPreset> DefaultPresets = new List<DnsPreset>
        {
            new DnsPreset { Name = "Automatic", Ipv4Primary = "0.0.0.0", Ipv4Secondary = "0.0.0.0", Ipv6Primary = "", Ipv6Secondary = "" },
            new DnsPreset { Name = "Cloudflare (1.1.1.1)", Ipv4Primary = "1.1.1.1", Ipv4Secondary = "1.0.0.1", Ipv6Primary = "2606:4700:4700::1111", Ipv6Secondary = "2606:4700:4700::1001" },
            new DnsPreset { Name = "Google Public DNS", Ipv4Primary = "8.8.8.8", Ipv4Secondary = "8.8.4.4", Ipv6Primary = "2001:4860:4860::8888", Ipv6Secondary = "2001:4860:4860::8844" },
            new DnsPreset { Name = "Verisign (Global Stability)", Ipv4Primary = "64.6.64.6", Ipv4Secondary = "64.6.65.6", Ipv6Primary = "2620:74:1b::1:1", Ipv6Secondary = "2620:74:1b::1:2" },
            new DnsPreset { Name = "Level 3 (High Speed)", Ipv4Primary = "209.244.0.3", Ipv4Secondary = "209.244.0.4", Ipv6Primary = "::", Ipv6Secondary = "::" },
            new DnsPreset { Name = "Freenom World", Ipv4Primary = "80.80.80.80", Ipv4Secondary = "80.80.81.81", Ipv6Primary = "::", Ipv6Secondary = "::" },
            new DnsPreset { Name = "Quad9 (Security)", Ipv4Primary = "9.9.9.9", Ipv4Secondary = "149.112.112.112", Ipv6Primary = "2620:fe::fe", Ipv6Secondary = "2620:fe::9" },
            new DnsPreset { Name = "Neustar UltraSecurity", Ipv4Primary = "156.154.70.3", Ipv4Secondary = "156.154.71.3", Ipv6Primary = "2620:12c:0000::0503", Ipv6Secondary = "2620:12c:0000::0502" },
            new DnsPreset { Name = "Comodo Secure DNS", Ipv4Primary = "8.26.56.26", Ipv4Secondary = "8.20.247.20", Ipv6Primary = "::", Ipv6Secondary = "::" },
            new DnsPreset { Name = "AdGuard DNS (Default)", Ipv4Primary = "94.140.14.14", Ipv4Secondary = "94.140.15.15", Ipv6Primary = "2a10:50c0::ad1:ff", Ipv6Secondary = "2a10:50c0::ad2:ff" },
            new DnsPreset { Name = "AdGuard DNS (Family)", Ipv4Primary = "94.140.14.15", Ipv4Secondary = "94.140.15.16", Ipv6Primary = "2a10:50c0::bad1:ff", Ipv6Secondary = "2a10:50c0::bad2:ff" },
            new DnsPreset { Name = "AdGuard DNS (Non filtering)", Ipv4Primary = "94.140.14.140", Ipv4Secondary = "94.140.14.141", Ipv6Primary = "2a10:50c0::1:ff", Ipv6Secondary = "2a10:50c0::2:ff" },
            new DnsPreset { Name = "OpenDNS Home", Ipv4Primary = "208.67.222.222", Ipv4Secondary = "208.67.220.220", Ipv6Primary = "::", Ipv6Secondary = "::" },
            new DnsPreset { Name = "CleanBrowsing (Family)", Ipv4Primary = "185.228.168.168", Ipv4Secondary = "185.228.169.168", Ipv6Primary = "2a0d:2a00:1::", Ipv6Secondary = "2a0d:2a00:2::" },
            new DnsPreset { Name = "CleanBrowsing (Adult filter)", Ipv4Primary = "185.228.168.10", Ipv4Secondary = "185.228.168.11", Ipv6Primary = "2a0d:2a00:1::1", Ipv6Secondary = "2a0d:2a00:2::1" },
            new DnsPreset { Name = "NextDNS", Ipv4Primary = "45.90.28.119", Ipv4Secondary = "45.90.30.119", Ipv6Primary = "2a07:a8c0::4e:9d2a", Ipv6Secondary = "2a07:a8c1::4e:9d2a" },
            new DnsPreset { Name = "AlternateDNS", Ipv4Primary = "76.76.19.19", Ipv4Secondary = "76.223.122.150", Ipv6Primary = "2602:fcbc::ad", Ipv6Secondary = "2602:fcbc:2::ad" },
            new DnsPreset { Name = "DNSCrypt", Ipv4Primary = "127.0.0.1", Ipv4Secondary = "", Ipv6Primary = "::1", Ipv6Secondary = "" },
            new DnsPreset { Name = "Yandex DNS (Safe)", Ipv4Primary = "77.88.8.88", Ipv4Secondary = "77.88.8.2", Ipv6Primary = "2a02:6b8::bad", Ipv6Secondary = "2a02:6b8::bda" },
            new DnsPreset { Name = "Custom", Ipv4Primary = "", Ipv4Secondary = "", Ipv6Primary = "", Ipv6Secondary = "" }
        };
    }
}
