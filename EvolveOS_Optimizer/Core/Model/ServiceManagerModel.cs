namespace EvolveOS_Optimizer.Core.Model
{
    internal class ServiceManagerModel
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StartType { get; set; } = string.Empty;
        public bool CanStart
        {
            get; set;
        }
        public bool CanStop
        {
            get; set;
        }

        public string StatusIcon => Status == "Running" ? "\uE768" : "\uE71A";

        public int StartTypeIndex => StartType switch
        {
            "Automatic" => 0,
            "Manual" => 1,
            "Disabled" => 2,
            _ => 1
        };
    }
}
