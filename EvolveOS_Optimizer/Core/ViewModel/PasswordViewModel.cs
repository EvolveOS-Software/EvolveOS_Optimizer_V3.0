// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Core.ViewModel
{
    public class PasswordViewModel : INotifyPropertyChanged
    {
        private readonly PasswordValidator _validator = new PasswordValidator();
        private string _password = "";
        private double _validationProgress;

        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged(nameof(Password));
                ValidateAndUpdateProgress();
            }
        }

        public double ValidationProgress
        {
            get => _validationProgress;
            set
            {
                _validationProgress = value;
                OnPropertyChanged(nameof(ValidationProgress));
                OnPropertyChanged(nameof(ProgressText));
            }
        }

        public bool IsPasswordStrong => ValidationRules.All(r => r.IsMet);

        public string ProgressText => $"{(int)ValidationProgress}%";

        public ObservableCollection<ValidationRule> ValidationRules { get; set; } =
        new ObservableCollection<ValidationRule>();

        public PasswordViewModel()
        {
            UpdateValidationRules(_validator.Validate(string.Empty));
        }

        private void ValidateAndUpdateProgress()
        {
            var results = _validator.Validate(Password);
            UpdateValidationRules(results);

            double metRules = results.Count(r => r.IsMet);
            double totalRules = results.Count;
            ValidationProgress = (metRules / totalRules) * 100;

            OnPropertyChanged(nameof(IsPasswordStrong));
        }

        private void UpdateValidationRules(List<ValidationRule> newRules)
        {
            ValidationRules.Clear();
            foreach (var rule in newRules)
            {
                ValidationRules.Add(rule);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}