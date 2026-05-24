// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Utilities.Services;

namespace EvolveOS_Optimizer.Dialogs
{
    public sealed partial class AiAnalysisDialog : ContentDialog
    {
        public AiAnalysisDialog()
        {
            this.InitializeComponent();
        }

        public async Task ShowAndAnalyzeAsync(string name, string category, string details)
        {
            var showTask = this.ShowAsync().AsTask();

            string result = await AiExplainerService.ExplainGenericItemAsync(name, category, details);

            LoadingPanel.Visibility = Visibility.Collapsed;
            ResultScrollViewer.Visibility = Visibility.Visible;
            AiResponseText.Text = result;
        }
    }
}