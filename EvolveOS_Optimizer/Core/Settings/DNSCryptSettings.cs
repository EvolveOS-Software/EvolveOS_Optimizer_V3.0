using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Utilities.Helpers;
using static EvolveOS_Optimizer.Core.Enums;
using ComboBoxItem = EvolveOS_Optimizer.Core.Structs.ComboBoxItem;

namespace EvolveOS_Optimizer.Core.Settings
{
    public class DNSCryptSetting_ipv4_servers : IDNSCryptSetting
    {
        private const string Name = "ipv4_servers";

        public string GetCurrentSetting(string config) => DNSCryptHelper.GetCurrentSetting(config, Name);

        public string? GetSetting(DNSSettingPreference preference = DNSSettingPreference.Recommended) => null;

        public IEnumerable<ComboBoxItem> GetSettings(string config)
        {
            return new[]
            {
                new ComboBoxItem("true"),
                new ComboBoxItem("false"),
            };
        }

        public string SetSetting(string config, string value) => DNSCryptHelper.SetSetting(config, Name, value);
    }

    public class DNSCryptSetting_ipv6_servers : IDNSCryptSetting
    {
        private const string Name = "ipv6_servers";

        public string GetCurrentSetting(string config) => DNSCryptHelper.GetCurrentSetting(config, Name);

        public string? GetSetting(DNSSettingPreference preference = DNSSettingPreference.Recommended) => null;

        public IEnumerable<ComboBoxItem> GetSettings(string config)
        {
            return new[]
            {
                new ComboBoxItem("true"),
                new ComboBoxItem("false"),
            };
        }

        public string SetSetting(string config, string value) => DNSCryptHelper.SetSetting(config, Name, value);
    }

    public class DNSCryptSetting_block_ipv6 : IDNSCryptSetting
    {
        private const string Name = "block_ipv6";

        public string GetCurrentSetting(string config) => DNSCryptHelper.GetCurrentSetting(config, Name);

        public string? GetSetting(DNSSettingPreference preference = DNSSettingPreference.Recommended) => null;

        public IEnumerable<ComboBoxItem> GetSettings(string config)
        {
            return new[]
            {
                new ComboBoxItem("true"),
                new ComboBoxItem("false"),
            };
        }

        public string SetSetting(string config, string value) => DNSCryptHelper.SetSetting(config, Name, value);
    }

    public class DNSCryptSetting_dnscrypt_servers : IDNSCryptSetting
    {
        private const string Name = "dnscrypt_servers";

        public string GetCurrentSetting(string config) => DNSCryptHelper.GetCurrentSetting(config, Name);

        public string? GetSetting(DNSSettingPreference preference = DNSSettingPreference.Recommended) => "true";

        public IEnumerable<ComboBoxItem> GetSettings(string config)
        {
            return new[]
            {
                new ComboBoxItem("true"),
                new ComboBoxItem("false"),
            };
        }

        public string SetSetting(string config, string value) => DNSCryptHelper.SetSetting(config, Name, value);
    }

    public class DNSCryptSetting_doh_servers : IDNSCryptSetting
    {
        private const string Name = "doh_servers";

        public string GetCurrentSetting(string config) => DNSCryptHelper.GetCurrentSetting(config, Name);

        public string? GetSetting(DNSSettingPreference preference = DNSSettingPreference.Recommended)
        {
            if (preference == DNSSettingPreference.Privacy)
            {
                return "false";
            }
            return "true";
        }

        public IEnumerable<ComboBoxItem> GetSettings(string config)
        {
            return new[]
            {
                new ComboBoxItem("true"),
                new ComboBoxItem("false"),
            };
        }

        public string SetSetting(string config, string value) => DNSCryptHelper.SetSetting(config, Name, value);
    }

    public class DNSCryptSetting_dnscrypt_ephemeral_keys : IDNSCryptSetting
    {
        private const string Name = "dnscrypt_ephemeral_keys";

        public string GetCurrentSetting(string config) => DNSCryptHelper.GetCurrentSetting(config, Name);

        public string? GetSetting(DNSSettingPreference preference = DNSSettingPreference.Recommended)
        {
            if (preference == DNSSettingPreference.Privacy)
            {
                return "true";
            }
            return "false";
        }

        public IEnumerable<ComboBoxItem> GetSettings(string config)
        {
            return new[]
            {
                new ComboBoxItem("true"),
                new ComboBoxItem("false"),
            };
        }

        public string SetSetting(string config, string value) => DNSCryptHelper.SetSetting(config, Name, value);
    }

    public class DNSCryptSetting_bootstrap_resolvers : IDNSCryptSetting
    {
        private const string Name = "bootstrap_resolvers";

        public string GetCurrentSetting(string config) => DNSCryptHelper.GetCurrentSetting(config, Name);

        public string? GetSetting(DNSSettingPreference preference = DNSSettingPreference.Recommended)
        {
            return "['1.1.1.1:53', '1.0.0.1:53']";
        }

        public IEnumerable<ComboBoxItem> GetSettings(string config)
        {
            var currentSetting = GetCurrentSetting(config);
            var setting = GetSetting();

            if (currentSetting == setting)
            {
                return new[]
                {
                    new ComboBoxItem("Cloudflare", setting!),
                };
            }

            return new[]
            {
                new ComboBoxItem(currentSetting),
                new ComboBoxItem("Cloudflare", setting!),
            };
        }

        public string SetSetting(string config, string value) => DNSCryptHelper.SetSetting(config, Name, value);
    }

    public class DNSCryptSetting_reject_ttl : IDNSCryptSetting
    {
        private const string Name = "reject_ttl";

        public string GetCurrentSetting(string config) => DNSCryptHelper.GetCurrentSetting(config, Name);

        public string? GetSetting(DNSSettingPreference preference = DNSSettingPreference.Recommended) => "3600";

        public IEnumerable<ComboBoxItem> GetSettings(string config)
        {
            var currentSetting = GetCurrentSetting(config);
            var setting = GetSetting();

            if (currentSetting == setting)
            {
                return new[]
                {
                    new ComboBoxItem(setting),
                };
            }

            return new[]
            {
                new ComboBoxItem(currentSetting),
                new ComboBoxItem(setting!),
            };
        }

        public string SetSetting(string config, string value) => DNSCryptHelper.SetSetting(config, Name, value);
    }

    public class DNSCryptSetting_netprobe_timeout : IDNSCryptSetting
    {
        private const string Name = "netprobe_timeout";

        public string GetCurrentSetting(string config) => DNSCryptHelper.GetCurrentSetting(config, Name);

        public string? GetSetting(DNSSettingPreference preference = DNSSettingPreference.Recommended) => "-1";

        public IEnumerable<ComboBoxItem> GetSettings(string config)
        {
            var currentSetting = GetCurrentSetting(config);
            var setting = GetSetting();

            if (currentSetting == setting)
            {
                return new[]
                {
                    new ComboBoxItem("-1"),
                };
            }

            return new[]
            {
                new ComboBoxItem(currentSetting),
                new ComboBoxItem("-1"),
            };
        }

        public string SetSetting(string config, string value) => DNSCryptHelper.SetSetting(config, Name, value);
    }

    public class DNSCryptSetting_netprobe_address : IDNSCryptSetting
    {
        private const string Name = "netprobe_address";

        public string GetCurrentSetting(string config) => DNSCryptHelper.GetCurrentSetting(config, Name);

        public string? GetSetting(DNSSettingPreference preference = DNSSettingPreference.Recommended) => "'1.1.1.1:53'";

        public IEnumerable<ComboBoxItem> GetSettings(string config)
        {
            var currentSetting = GetCurrentSetting(config);
            var setting = GetSetting();

            if (currentSetting == setting)
            {
                return new[]
                {
                    new ComboBoxItem("Cloudflare", "'1.1.1.1:53'"),
                };
            }

            return new[]
            {
                new ComboBoxItem(currentSetting),
                new ComboBoxItem("Cloudflare", "'1.1.1.1:53'"),
            };
        }

        public string SetSetting(string config, string value) => DNSCryptHelper.SetSetting(config, Name, value);
    }

    public class DNSCryptSetting_require_nofilter : IDNSCryptSetting
    {
        private const string Name = "require_nofilter";

        public string GetCurrentSetting(string config) => DNSCryptHelper.GetCurrentSetting(config, Name);

        public string? GetSetting(DNSSettingPreference preference = DNSSettingPreference.Recommended) => "true";

        public IEnumerable<ComboBoxItem> GetSettings(string config)
        {
            return new[]
            {
                new ComboBoxItem("true"),
                new ComboBoxItem("false"),
            };
        }

        public string SetSetting(string config, string value) => DNSCryptHelper.SetSetting(config, Name, value);
    }

    public class DNSCryptSetting_require_dnssec : IDNSCryptSetting
    {
        private const string Name = "require_dnssec";

        public string GetCurrentSetting(string config) => DNSCryptHelper.GetCurrentSetting(config, Name);

        public string? GetSetting(DNSSettingPreference preference = DNSSettingPreference.Recommended) => "true";

        public IEnumerable<ComboBoxItem> GetSettings(string config)
        {
            return new[]
            {
                new ComboBoxItem("true"),
                new ComboBoxItem("false"),
            };
        }

        public string SetSetting(string config, string value) => DNSCryptHelper.SetSetting(config, Name, value);
    }

    public class DNSCryptSetting_require_nolog : IDNSCryptSetting
    {
        private const string Name = "require_nolog";

        public string GetCurrentSetting(string config) => DNSCryptHelper.GetCurrentSetting(config, Name);

        public string? GetSetting(DNSSettingPreference preference = DNSSettingPreference.Recommended) => "true";

        public IEnumerable<ComboBoxItem> GetSettings(string config)
        {
            return new[]
            {
                new ComboBoxItem("true"),
                new ComboBoxItem("false"),
            };
        }

        public string SetSetting(string config, string value) => DNSCryptHelper.SetSetting(config, Name, value);
    }

    public class DNSCryptSetting_tls_disable_session_tickets : IDNSCryptSetting
    {
        private const string Name = "tls_disable_session_tickets";

        public string GetCurrentSetting(string config) => DNSCryptHelper.GetCurrentSetting(config, Name);

        public string? GetSetting(DNSSettingPreference preference = DNSSettingPreference.Recommended)
        {
            return preference == DNSSettingPreference.Privacy ? "true" : "false";
        }

        public IEnumerable<ComboBoxItem> GetSettings(string config)
        {
            return new[]
            {
                new ComboBoxItem("true"),
                new ComboBoxItem("false"),
            };
        }

        public string SetSetting(string config, string value) => DNSCryptHelper.SetSetting(config, Name, value);
    }
}