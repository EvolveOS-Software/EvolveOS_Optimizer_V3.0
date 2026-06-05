// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Utilities.Controls;

namespace EvolveOS_Optimizer.Dialogs
{
    public sealed partial class CloudConnectDialog : ContentDialog
    {
        #region Constructor
        public CloudConnectDialog()
        {
            this.InitializeComponent();

            LoadInitialSettings();

            CmbProvider.SelectionChanged += CmbProvider_SelectionChanged;
        }
        #endregion

        #region Initialization
        private void LoadInitialSettings()
        {
            var activeProvider = LocalMachineSettingsEngine.ActiveAiProvider;

            foreach (ComboBoxItem item in CmbProvider.Items)
            {
                if (item.Tag.ToString() == activeProvider.ToString())
                {
                    CmbProvider.SelectedItem = item;

                    UpdatePasswordBox(activeProvider);
                    break;
                }
            }
        }
        #endregion

        #region Event Handlers
        private void CmbProvider_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.RemovedItems.Count > 0 && e.RemovedItems[0] is ComboBoxItem oldItem)
            {
                if (Enum.TryParse<Core.Enums.AiProvider>(oldItem.Tag.ToString(), out var oldProvider))
                {
                    SaveKeyToEngine(oldProvider, TxtApiKey.Password);
                }
            }

            if (CmbProvider.SelectedItem is ComboBoxItem item && Enum.TryParse<Core.Enums.AiProvider>(item.Tag.ToString(), out var provider))
            {
                TxtApiKey.Password = GetKeyForProvider(provider);
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (CmbProvider.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string providerTag)
            {
                if (Enum.TryParse<Core.Enums.AiProvider>(providerTag, out var provider))
                {
                    LocalMachineSettingsEngine.ActiveAiProvider = provider;
                    SaveKeyToEngine(provider, TxtApiKey.Password);
                }
            }
        }

        private void BtnReveal_Checked(object sender, RoutedEventArgs e)
        {
            TxtApiKey.PasswordRevealMode = PasswordRevealMode.Visible;
            RevealIcon.Symbol = FluentIcons.Common.Symbol.EyeOff;
        }

        private void BtnReveal_Unchecked(object sender, RoutedEventArgs e)
        {
            TxtApiKey.PasswordRevealMode = PasswordRevealMode.Hidden;
            RevealIcon.Symbol = FluentIcons.Common.Symbol.Eye;
        }
        #endregion

        #region Helper Methods
        private void UpdatePasswordBox(AiProvider provider)
        {
            TxtApiKey.Password = GetKeyForProvider(provider);
        }

        private void SaveKeyToEngine(AiProvider provider, string key)
        {
            switch (provider)
            {
                case AiProvider.Gemini:
                    LocalMachineSettingsEngine.GeminiApiKey = key;
                    break;
                case AiProvider.Groq:
                    LocalMachineSettingsEngine.GroqApiKey = key;
                    break;
                case AiProvider.OpenRouter:
                    LocalMachineSettingsEngine.OpenRouterApiKey = key;
                    break;
                case AiProvider.Cohere:
                    LocalMachineSettingsEngine.CohereApiKey = key;
                    break;
                case AiProvider.Mistral:
                    LocalMachineSettingsEngine.MistralApiKey = key;
                    break;
            }
        }

        private string GetKeyForProvider(AiProvider provider) => provider switch
        {
            AiProvider.Groq => LocalMachineSettingsEngine.GroqApiKey ?? "",
            AiProvider.Gemini => LocalMachineSettingsEngine.GeminiApiKey ?? "",
            AiProvider.OpenRouter => LocalMachineSettingsEngine.OpenRouterApiKey ?? "",
            AiProvider.Cohere => LocalMachineSettingsEngine.CohereApiKey ?? "",
            AiProvider.Mistral => LocalMachineSettingsEngine.MistralApiKey ?? "",
            _ => ""
        };
        #endregion
    }
}