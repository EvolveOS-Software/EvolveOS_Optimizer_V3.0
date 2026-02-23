using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Core.ViewModel.Items
{
    public sealed class PolicyStateViewModel
    {
        private readonly GroupPolicyHelper.PolicyState _state;

        public PolicyStateViewModel(GroupPolicyHelper.PolicyState state)
        {
            _state = state;
        }

        public GroupPolicyHelper.PolicyEntry Policy => _state.Policy;

        public string HiveDisplay => _state.Policy.Hive switch
        {
            Microsoft.Win32.RegistryHive.LocalMachine => "HKLM",
            Microsoft.Win32.RegistryHive.CurrentUser => "HKCU",
            _ => _state.Policy.Hive.ToString()
        };

        public string CurrentValueDisplay
        {
            get
            {
                if (_state.CurrentValue == null)
                    return ResourceString.GetString("Not set");

                return _state.ActualValueKind switch
                {
                    Microsoft.Win32.RegistryValueKind.DWord => $"{ResourceString.GetString("Value")}: {_state.CurrentValue}",
                    Microsoft.Win32.RegistryValueKind.String => $"{ResourceString.GetString("Value")}: \"{_state.CurrentValue}\"",
                    _ => $"{ResourceString.GetString("Value")}: {_state.CurrentValue}"
                };
            }
        }
    }
}