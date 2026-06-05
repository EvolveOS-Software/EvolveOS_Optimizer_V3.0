using EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Core.Interfaces
{
    public interface IDNSCryptSetting
    {
        string? GetSetting(DNSSettingPreference preference = DNSSettingPreference.Recommended);
        IEnumerable<Structs.ComboBoxItem> GetSettings(string config);

        string GetCurrentSetting(string config);
        string SetSetting(string config, string value);
    }
}
