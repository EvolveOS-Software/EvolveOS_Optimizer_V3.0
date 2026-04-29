// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model
{
    public class ServiceAnomaly
    {
        public string ServiceName { get; set; } = string.Empty;
        public string FriendlyName { get; set; } = string.Empty;
        public string AnomalyType { get; set; } = string.Empty;
        public int RecommendedEventId { get; set; }
        public string AlertMessage { get; set; } = string.Empty;
    }
}
