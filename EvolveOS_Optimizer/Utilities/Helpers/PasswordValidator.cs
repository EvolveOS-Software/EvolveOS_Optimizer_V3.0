// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public class ValidationRule
    {
        public string? Name { get; set; }
        public bool IsMet { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class PasswordValidator
    {
        public const int MinLength = 8;
        public const int MaxLength = 128;

        public List<ValidationRule> Validate(string password)
        {
            var rules = new List<ValidationRule>();

            rules.Add(new ValidationRule
            {
                Name = string.Format(
                    ResourceHelper.GetResourceString("val_length_name"),
                    MinLength,
                    MaxLength),
                IsMet = password.Length >= MinLength && password.Length <= MaxLength,

                ErrorMessage = string.Format(
                    ResourceHelper.GetResourceString("val_length_err"),
                    MinLength,
                    MaxLength)
            });

            rules.Add(new ValidationRule
            {
                Name = ResourceHelper.GetResourceString("val_uppercase_name"),
                IsMet = password.Any(char.IsUpper),
                ErrorMessage = ResourceHelper.GetResourceString("val_uppercase_err_full")
            });

            rules.Add(new ValidationRule
            {
                Name = ResourceHelper.GetResourceString("val_lowercase_name"),
                IsMet = password.Any(char.IsLower),
                ErrorMessage = ResourceHelper.GetResourceString("val_lowercase_err")
            });

            rules.Add(new ValidationRule
            {
                Name = ResourceHelper.GetResourceString("val_digit_name"),
                IsMet = password.Any(char.IsDigit),
                ErrorMessage = ResourceHelper.GetResourceString("val_digit_err")
            });

            rules.Add(new ValidationRule
            {
                Name = ResourceHelper.GetResourceString("val_special_name"),
                IsMet = Regex.IsMatch(password, @"[^a-zA-Z0-9\s]"),
                ErrorMessage = ResourceHelper.GetResourceString("val_special_err_full")
            });

            rules.Add(new ValidationRule
            {
                Name = ResourceHelper.GetResourceString("val_patterns_name"),
                IsMet = !Regex.IsMatch(password, @"(.)\1{3,}"),
                ErrorMessage = ResourceHelper.GetResourceString("val_patterns_err")
            });

            return rules;
        }
    }
}