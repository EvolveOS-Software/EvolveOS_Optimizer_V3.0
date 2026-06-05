// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Documents;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Dialogs;

internal class TaskOutputDialogBuilder
{
    private readonly ILocalizationService _localization;
    private readonly ITaskProgressService _taskProgressService;

    private bool _isSubscribed;
    private bool _lastLineWasProgress;
    private int _lastLineRunCount = 1;
    private EventHandler<TaskProgressDetail>? _liveHandler;
    private Paragraph _paragraph = null!;
    private ScrollViewer _scrollViewer = null!;
    private List<string> _allLines = null!;

    public TaskOutputDialogBuilder(ILocalizationService localization, ITaskProgressService taskProgressService)
    {
        _localization = localization;
        _taskProgressService = taskProgressService;
    }

    public ContentDialog Build(XamlRoot xamlRoot, string title, IReadOnlyList<string> logMessages)
    {
        _allLines = new List<string>(logMessages);

        _paragraph = new Paragraph();
        foreach (var line in logMessages)
        {
            var runs = TerminalLineRenderer.CreateLineRuns(line);
            foreach (var run in runs)
                _paragraph.Inlines.Add(run);
            _lastLineRunCount = runs.Length;
            _lastLineWasProgress = TerminalLineRenderer.LooksLikeProgressBar(line);
        }

        var richTextBlock = new RichTextBlock
        {
            FontFamily = TerminalLineRenderer.MonoFont,
            FontSize = 12,
            Foreground = TerminalLineRenderer.DefaultBrush,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true
        };
        richTextBlock.Blocks.Add(_paragraph);

        _scrollViewer = new ScrollViewer
        {
            Content = richTextBlock,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(14, 10, 14, 10)
        };

        _scrollViewer.Loaded += (_, _) =>
            _scrollViewer.ChangeView(null, _scrollViewer.ScrollableHeight, null, true);

        var container = new Border
        {
            Child = _scrollViewer,
            Background = new SolidColorBrush(TerminalLineRenderer.TerminalBackground),
            CornerRadius = new CornerRadius(6),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x3E, 0x3E, 0x3E)),
            BorderThickness = new Thickness(1)
        };

        var dialog = new ContentDialog
        {
            Title = title,
            Content = container,
            CloseButtonText = _localization.GetString("Button_Close") ?? "Close",
            SecondaryButtonText = _localization.GetString("Button_CopyToClipboard") ?? "Copy to Clipboard",
            DefaultButton = ContentDialogButton.Close
        };

        dialog.Resources["ContentDialogMaxWidth"] = 8192;
        dialog.Resources["ContentDialogMaxHeight"] = 4096;

        dialog.SizeChanged += (_, _) =>
        {
            if (dialog.Content is FrameworkElement content && xamlRoot.Size.Width > 0)
            {
                double winWidth = xamlRoot.Size.Width;
                double winHeight = xamlRoot.Size.Height;

                content.Width = Math.Min(Math.Max(600, winWidth * 0.90) - 48, 8192);
                content.Height = Math.Min(Math.Max(300, winHeight * 0.70) - 120, 4096);
            }
        };

        dialog.SecondaryButtonClick += (_, _) =>
        {
            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(string.Join("\n", _allLines));
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
        };

        return dialog;
    }

    public void StartLiveUpdates(DispatcherQueue dispatcherQueue)
    {
        if (!_taskProgressService.IsTaskRunning)
            return;

        _isSubscribed = true;

        _liveHandler = (_, detail) =>
        {
            if (string.IsNullOrEmpty(detail.TerminalOutput))
                return;

            var line = detail.TerminalOutput;
            var isProgress = detail.IsProgressIndicator;

            dispatcherQueue.TryEnqueue(() =>
            {
                if (_lastLineWasProgress && _paragraph.Inlines.Count > 0)
                {
                    for (int r = 0; r < _lastLineRunCount && _paragraph.Inlines.Count > 0; r++)
                        _paragraph.Inlines.RemoveAt(_paragraph.Inlines.Count - 1);
                    _allLines.RemoveAt(_allLines.Count - 1);
                }
                else if (isProgress && _allLines.Count > 0
                    && TerminalLineRenderer.LooksLikeProgressBar(_allLines[_allLines.Count - 1]))
                {
                    for (int r = 0; r < _lastLineRunCount && _paragraph.Inlines.Count > 0; r++)
                        _paragraph.Inlines.RemoveAt(_paragraph.Inlines.Count - 1);
                    _allLines.RemoveAt(_allLines.Count - 1);
                }

                _allLines.Add(line);
                var runs = TerminalLineRenderer.CreateLineRuns(line);
                foreach (var run in runs)
                    _paragraph.Inlines.Add(run);
                _lastLineRunCount = runs.Length;
                _lastLineWasProgress = isProgress;

                _scrollViewer.UpdateLayout();
                var isNearBottom = _scrollViewer.VerticalOffset
                    >= _scrollViewer.ScrollableHeight - 20;
                if (isNearBottom)
                    _scrollViewer.ChangeView(null, _scrollViewer.ScrollableHeight, null, true);
            });

            if (!_taskProgressService.IsTaskRunning)
            {
                if (_isSubscribed)
                {
                    _isSubscribed = false;
                    _taskProgressService.ProgressUpdated -= _liveHandler;
                }
            }
        };

        _taskProgressService.ProgressUpdated += _liveHandler;
    }
    public void StopLiveUpdates()
    {
        if (_isSubscribed && _liveHandler != null)
        {
            _isSubscribed = false;
            _taskProgressService.ProgressUpdated -= _liveHandler;
        }
    }
}
