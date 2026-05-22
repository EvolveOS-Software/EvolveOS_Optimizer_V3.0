// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Base;

namespace EvolveOS_Optimizer.Core.Model
{
    public sealed class SystemModel : ViewModelBase, ITypedPageItem<double>
    {
        public string Name { get; set; } = string.Empty;

        private bool _state;
        public bool State
        {
            get => _state;
            set
            {
                if (SetProperty(ref _state, value))
                {
                    OnPropertyChanged(nameof(IsRecommendedVisible));
                }
            }
        }

        private bool _recommendedState;
        public bool RecommendedState
        {
            get => _recommendedState;
            set
            {
                if (SetProperty(ref _recommendedState, value))
                {
                    OnPropertyChanged(nameof(IsRecommendedVisible));
                }
            }
        }
        public bool IsRecommendedVisible => State != RecommendedState;

        public double Value { get; set; }
        public bool IsFaulted { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}