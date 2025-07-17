using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WhisperTranscriberCLI.Core.Models;
using WhisperTranscriberCLI.Core.Services;
using WhisperTranscriberCLI.TaskUI.Services;
using WhisperTranscriberCLI.TaskUI.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Navigation;
using Windows.UI.Text;Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WhisperTranscriberCLI.Core.Models;
using WhisperTranscriberCLI.Core.Services;
using WhisperTranscriberCLI.TaskUI.Services;
using WhisperTranscriberCLI.TaskUI.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Navigation;

namespace WhisperTranscriberCLI.TaskUI.Views;

public sealed partial class MainPage : Page
{
    private readonly ObservableCollection<TaskViewModel> _tasks = new();
    private readonly ModelDiscovery _modelDiscovery;
    private readonly AudioDurationService _audioDurationService;
    private readonly SettingsService _settingsService;
    private readonly SystemCheckService _systemCheckService;
    private readonly SystemTrayService _systemTrayService;
    private QueueManager? _queueManager;
    
    public MainPage()
    {
        this.InitializeComponent();
        _modelDiscovery = new ModelDiscovery();
        _audioDurationService = new AudioDurationService();
        _settingsService = new SettingsService();
        _systemCheckService = new SystemCheckService();
        _systemTrayService = new SystemTrayService(App.MainWindow, _settingsService);
        
        InitializeSystemTray();
        
        TaskListView.ItemsSource = _tasks;
        LoadModels();
        LoadSettings();
        InitializeQueueManager();
        InitializeTheme();
        _ = CheckSystemRequirementsAsync();
    }

    private void LoadModels()
    {
        var models = _modelDiscovery.GetAvailableModels();
        ModelComboBox.ItemsSource = models;
        ModelComboBox.DisplayMemberPath = "Name";
        ModelComboBox.SelectedValuePath = "Name";
        
        if (models.Count > 0)
        {
            // Try to select the saved default model
            var defaultModel = models.FirstOrDefault(m => m.Name == _settingsService.Settings.DefaultModel);
            if (defaultModel != null)
            {
                ModelComboBox.SelectedItem = defaultModel;
            }
            else
            {
                ModelComboBox.SelectedIndex = 0;
            }
        }
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Settings;
        
        // Load UI settings
        OutputDirectoryTextBox.Text = settings.OutputDirectory;
        RecursiveCheckBox.IsChecked = settings.Recursive;
        
        // Set default language
        var languageItems = LanguageComboBox.Items.Cast<ComboBoxItem>();
        var defaultLanguageItem = languageItems.FirstOrDefault(item => 
            item.Content.ToString() == settings.DefaultLanguage);
        if (defaultLanguageItem != null)
        {
            LanguageComboBox.SelectedItem = defaultLanguageItem;
        }
    }

    private void InitializeTheme()
    {
        // Set the app theme to follow system theme
        if (App.MainWindow.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = Microsoft.UI.Xaml.ElementTheme.Default;
        }
    }

    private async Task CheckSystemRequirementsAsync()
    {
        try
        {
            UpdateStatus("Checking system requirements...");
            var result = await _systemCheckService.CheckSystemRequirementsAsync();
            
            if (result.IsSystemReady)
            {
                UpdateStatus("System ready for transcription");
            }
            else
            {
                UpdateStatus(result.GetStatusMessage());
                await ShowSystemRequirementsDialog(result);
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"System check failed: {ex.Message}");
        }
    }

    private async Task ShowSystemRequirementsDialog(SystemCheckResult result)
    {
        var content = new StackPanel { Spacing = 12 };
        
        content.Children.Add(new TextBlock
        {
            Text = "System Requirements Check",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold
        });
        
        content.Children.Add(new TextBlock
        {
            Text = result.GetStatusMessage(),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });
        
        if (!result.FFmpegAvailable)
        {
            content.Children.Add(new TextBlock
            {
                Text = "FFmpeg Installation:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            content.Children.Add(new TextBlock
            {
                Text = _systemCheckService.GetFFmpegInstallationInstructions(),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });
        }
        
        if (!result.WhisperModelsAvailable)
        {
            content.Children.Add(new TextBlock
            {
                Text = "Whisper Models:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            content.Children.Add(new TextBlock
            {
                Text = _systemCheckService.GetWhisperModelsInstructions(),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });
        }

        var dialog = new ContentDialog
        {
            Title = "System Requirements",
            Content = new ScrollViewer { Content = content },
            PrimaryButtonText = "Check Again",
            CloseButtonText = "Continue Anyway",
            XamlRoot = this.XamlRoot
        };

        var dialogResult = await dialog.ShowAsync();
        if (dialogResult == ContentDialogResult.Primary)
        {
            await CheckSystemRequirementsAsync();
        }
    }

    private void InitializeQueueManager()
    {
        try
        {
            var queuePath = Path.Combine(Directory.GetCurrentDirectory(), "shared", "TranscriptionQueue.json");
            var mediaConverter = new FfmpegMediaConverter();
            _queueManager = new QueueManager(queuePath, mediaConverter);
            
            _queueManager.StatusChanged += OnQueueStatusChanged;
            _queueManager.TaskCompleted += OnTaskCompleted;
            _queueManager.TaskFailed += OnTaskFailed;
            
            LoadExistingTasks();
        }
        catch (Exception ex)
        {
            UpdateStatus($"Failed to initialize queue manager: {ex.Message}");
        }
    }

    private void LoadExistingTasks()
    {
        if (_queueManager != null)
        {
            _tasks.Clear();
            foreach (var task in _queueManager.Queue.Tasks)
            {
                _tasks.Add(new TaskViewModel(task));
            }
        }
    }

    private async void AddFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.ViewMode = PickerViewMode.List;
        picker.FileTypeFilter.Add(".mp3");
        picker.FileTypeFilter.Add(".wav");
        picker.FileTypeFilter.Add(".mp4");
        picker.FileTypeFilter.Add(".avi");
        picker.FileTypeFilter.Add(".mkv");
        picker.FileTypeFilter.Add(".m4a");
        picker.FileTypeFilter.Add(".flac");
        picker.FileTypeFilter.Add(".ogg");
        picker.FileTypeFilter.Add(".webm");
        picker.FileTypeFilter.Add(".wma");

        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var files = await picker.PickMultipleFilesAsync();
        if (files != null && files.Count > 0)
        {
            await AddFilesToQueue(files.Select(f => f.Path));
        }
    }

    private async void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.ViewMode = PickerViewMode.List;

        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            await AddFolderToQueue(folder.Path);
        }
    }

    private async Task AddFilesToQueue(System.Collections.Generic.IEnumerable<string> filePaths)
    {
        var selectedModel = ModelComboBox.SelectedValue?.ToString() ?? "ggml-base.bin";
        var selectedLanguage = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "auto";
        var outputDirectory = OutputDirectoryTextBox.Text;

        foreach (var filePath in filePaths)
        {
            UpdateStatus($"Analyzing {Path.GetFileName(filePath)}...");
            var duration = await _audioDurationService.GetDurationAsync(filePath);
            var formattedDuration = _audioDurationService.FormatDuration(duration);
            
            var task = new TranscriptionTask
            {
                FilePath = filePath,
                ModelName = selectedModel,
                Language = selectedLanguage,
                Duration = formattedDuration,
                Status = Core.Models.TaskStatus.Pending
            };

            await _queueManager!.AddTaskAsync(task);
            _tasks.Add(new TaskViewModel(task));
        }

        UpdateStatus($"Added {filePaths.Count()} files to queue");
    }

    private async Task AddFolderToQueue(string folderPath)
    {
        var recursive = RecursiveCheckBox.IsChecked == true;
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        
        var supportedExtensions = new[] { ".mp3", ".wav", ".mp4", ".avi", ".mkv", ".m4a", ".flac", ".ogg", ".webm", ".wma" };
        var files = Directory.GetFiles(folderPath, "*.*", searchOption)
            .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLower()))
            .ToList();

        if (files.Any())
        {
            await AddFilesToQueue(files);
        }
        else
        {
            UpdateStatus("No supported audio/video files found in the selected folder");
        }
    }

    private async void StartQueueButton_Click(object sender, RoutedEventArgs e)
    {
        if (_queueManager != null)
        {
            await _queueManager.StartProcessingAsync();
            UpdateStatus("Queue processing started");
        }
    }

    private void PauseQueueButton_Click(object sender, RoutedEventArgs e)
    {
        if (_queueManager != null)
        {
            _queueManager.PauseProcessing();
            UpdateStatus("Queue processing paused");
        }
    }

    private void CancelCurrentButton_Click(object sender, RoutedEventArgs e)
    {
        if (_queueManager != null)
        {
            _queueManager.StopProcessing();
            UpdateStatus("Queue processing stopped");
        }
    }

    private async void ClearDoneButton_Click(object sender, RoutedEventArgs e)
    {
        if (_queueManager != null)
        {
            await _queueManager.ClearCompletedTasksAsync();
            
            var completedTasks = _tasks.Where(t => t.Status == "Done").ToList();
            foreach (var task in completedTasks)
            {
                _tasks.Remove(task);
            }
            
            UpdateStatus("Cleared completed tasks");
        }
    }

    private async void SaveQueueButton_Click(object sender, RoutedEventArgs e)
    {
        if (_queueManager != null)
        {
            await _queueManager.SaveQueueAsync();
            var queuePath = Path.Combine(Directory.GetCurrentDirectory(), "shared", "TranscriptionQueue.json");
            _settingsService.UpdateLastQueuePath(queuePath);
            UpdateStatus("Queue saved");
        }
    }

    private void LoadQueueButton_Click(object sender, RoutedEventArgs e)
    {
        if (_queueManager != null)
        {
            _queueManager.LoadQueue();
            LoadExistingTasks();
            UpdateStatus("Queue loaded");
        }
    }

    private async void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.ViewMode = PickerViewMode.List;

        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            OutputDirectoryTextBox.Text = folder.Path;
            _settingsService.UpdateOutputDirectory(folder.Path);
        }
    }

    private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is ModelInfo selectedModel)
        {
            _settingsService.UpdateDefaultModel(selectedModel.Name);
        }
    }

    private void RecursiveCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
        {
            _settingsService.UpdateRecursive(checkBox.IsChecked == true);
        }
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is ComboBoxItem selectedItem)
        {
            var language = selectedItem.Content.ToString() ?? "auto";
            _settingsService.UpdateDefaultLanguage(language);
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ShowSettingsPage();
    }

    private async void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        var aboutDialog = new ContentDialog
        {
            Title = "About Whisper Transcription Queue",
            Content = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Whisper Transcription Queue",
                            FontSize = 20,
                            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                            HorizontalAlignment = HorizontalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = "Version 1.0.0",
                            FontSize = 14,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 8)
                        },
                        new TextBlock
                        {
                            Text = "A modern WinUI 3 application for batch audio/video transcription using OpenAI Whisper.",
                            TextWrapping = TextWrapping.Wrap,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 8)
                        },
                        new TextBlock
                        {
                            Text = "Features:",
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            Margin = new Thickness(0, 8, 0, 4)
                        },
                        new TextBlock
                        {
                            Text = "• Batch transcription with queue management\n• Multiple audio/video format support\n• Real-time progress tracking\n• Automatic queue persistence\n• Keyboard shortcuts and context menus\n• Settings persistence",
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(16, 0, 0, 8)
                        },
                        new TextBlock
                        {
                            Text = "Supported Formats:",
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            Margin = new Thickness(0, 8, 0, 4)
                        },
                        new TextBlock
                        {
                            Text = "MP3, WAV, MP4, AVI, MKV, M4A, FLAC, OGG, WEBM, WMA",
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(16, 0, 0, 8)
                        },
                        new TextBlock
                        {
                            Text = "Requirements:",
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            Margin = new Thickness(0, 8, 0, 4)
                        },
                        new TextBlock
                        {
                            Text = "• Windows 10 version 1903 or later\n• .NET 8.0 Runtime\n• FFmpeg (for audio processing)\n• Whisper model files in whispermodels/ folder",
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(16, 0, 0, 8)
                        },
                        new TextBlock
                        {
                            Text = "🤖 Generated with Claude Code",
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 12, 0, 0)
                        }
                    }
                }
            },
            CloseButtonText = "Close",
            XamlRoot = this.XamlRoot
        };

        await aboutDialog.ShowAsync();
    }

    private void OnQueueStatusChanged(object? sender, Core.Events.TranscriptionStatusEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var taskViewModel = _tasks.FirstOrDefault(t => t.Id == e.TaskId);
            if (taskViewModel != null)
            {
                taskViewModel.Status = e.Status.ToString();
                taskViewModel.ErrorMessage = e.ErrorMessage;
                taskViewModel.OutputPath = e.OutputPath;
            }
        });
    }

    private void OnTaskCompleted(object? sender, TranscriptionTask e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateStatus($"Task completed: {Path.GetFileName(e.FilePath)}");
            UpdateQueueProgress();
            
            // Show system tray notification
            _systemTrayService.ShowNotification(
                "Transcription Complete", 
                $"Finished transcribing {Path.GetFileName(e.FilePath)}",
                NotificationSeverity.Success);
                
            // Open output if enabled
            if (_settingsService.Settings.OpenOutputAfterCompletion && !string.IsNullOrEmpty(e.OutputPath))
            {
                _ = OpenOutputFileAsync(e.OutputPath);
            }
        });
    }

    private void OnTaskFailed(object? sender, TranscriptionTask e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateStatus($"Task failed: {Path.GetFileName(e.FilePath)} - {e.ErrorMessage}");
            UpdateQueueProgress();
            
            // Show system tray notification
            _systemTrayService.ShowNotification(
                "Transcription Failed", 
                $"Failed to transcribe {Path.GetFileName(e.FilePath)}: {e.ErrorMessage}",
                NotificationSeverity.Error);
        });
    }

    private void UpdateQueueProgress()
    {
        if (_queueManager == null) return;
        
        var totalTasks = _queueManager.Queue.Tasks.Count;
        var completedTasks = _queueManager.Queue.Tasks.Count(t => t.Status == Core.Models.TaskStatus.Done);
        var failedTasks = _queueManager.Queue.Tasks.Count(t => t.Status == Core.Models.TaskStatus.Error);
        var processingTasks = _queueManager.Queue.Tasks.Count(t => t.Status == Core.Models.TaskStatus.Processing);
        var pendingTasks = _queueManager.Queue.Tasks.Count(t => t.Status == Core.Models.TaskStatus.Pending);
        
        if (totalTasks > 0)
        {
            var currentTask = _queueManager.Queue.Tasks.FirstOrDefault(t => t.Status == Core.Models.TaskStatus.Processing);
            if (currentTask != null)
            {
                var fileName = Path.GetFileName(currentTask.FilePath);
                var progress = $"Processing: {fileName} ({completedTasks + 1}/{totalTasks})";
                
                if (pendingTasks > 0)
                {
                    progress += $" • {pendingTasks} pending";
                }
                
                if (failedTasks > 0)
                {
                    progress += $" • {failedTasks} failed";
                }
                
                UpdateStatus(progress);
            }
            else if (pendingTasks > 0)
            {
                UpdateStatus($"Queue ready • {pendingTasks} pending, {completedTasks} completed");
            }
            else
            {
                UpdateStatus($"Queue complete • {completedTasks} completed, {failedTasks} failed");
                
                // Show queue completion notification
                if (completedTasks > 0 || failedTasks > 0)
                {
                    _systemTrayService.ShowNotification(
                        "Queue Complete", 
                        $"All tasks finished: {completedTasks} completed, {failedTasks} failed",
                        NotificationSeverity.Success);
                }
            }
        }
    }

    private void UpdateStatus(string message)
    {
        StatusTextBlock.Text = message;
    }

    // Context menu event handlers
    private void TaskListView_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var listView = sender as ListView;
        var item = (e.OriginalSource as FrameworkElement)?.DataContext as TaskViewModel;
        if (item != null)
        {
            listView.SelectedItem = item;
            
            // Update menu item visibility based on task status
            RetryTaskMenuItem.IsEnabled = item.Status == "Error";
            OpenOutputMenuItem.IsEnabled = !string.IsNullOrEmpty(item.OutputPath) && File.Exists(item.OutputPath);
            OpenFolderMenuItem.IsEnabled = !string.IsNullOrEmpty(item.OutputPath) && File.Exists(item.OutputPath);
        }
    }

    private async void RemoveTaskMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var selectedTask = TaskListView.SelectedItem as TaskViewModel;
        if (selectedTask != null && _queueManager != null)
        {
            await _queueManager.RemoveTaskAsync(selectedTask.Id);
            _tasks.Remove(selectedTask);
            UpdateStatus($"Removed task: {selectedTask.FileName}");
        }
    }

    private async void RetryTaskMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var selectedTask = TaskListView.SelectedItem as TaskViewModel;
        if (selectedTask != null && _queueManager != null)
        {
            // Find the task in the queue and reset its status
            var task = _queueManager.Queue.Tasks.FirstOrDefault(t => t.Id == selectedTask.Id);
            if (task != null)
            {
                task.Status = Core.Models.TaskStatus.Pending;
                task.ErrorMessage = null;
                task.Progress = 0;
                selectedTask.Status = "Pending";
                selectedTask.ErrorMessage = null;
                selectedTask.Progress = 0;
                
                await _queueManager.SaveQueueAsync();
                UpdateStatus($"Reset task for retry: {selectedTask.FileName}");
            }
        }
    }

    private async void OpenOutputMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var selectedTask = TaskListView.SelectedItem as TaskViewModel;
        if (selectedTask != null && !string.IsNullOrEmpty(selectedTask.OutputPath))
        {
            try
            {
                var storageFile = await StorageFile.GetFileFromPathAsync(selectedTask.OutputPath);
                await Windows.System.Launcher.LaunchFileAsync(storageFile);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Failed to open output: {ex.Message}");
            }
        }
    }

    private async void OpenFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var selectedTask = TaskListView.SelectedItem as TaskViewModel;
        if (selectedTask != null && !string.IsNullOrEmpty(selectedTask.OutputPath))
        {
            try
            {
                var folderPath = Path.GetDirectoryName(selectedTask.OutputPath);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    var storageFolder = await StorageFolder.GetFolderFromPathAsync(folderPath);
                    await Windows.System.Launcher.LaunchFolderAsync(storageFolder);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Failed to open folder: {ex.Message}");
            }
        }
    }

    private void CopyPathMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var selectedTask = TaskListView.SelectedItem as TaskViewModel;
        if (selectedTask != null)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(selectedTask.FilePath);
            Clipboard.SetContent(dataPackage);
            UpdateStatus($"Copied path to clipboard: {selectedTask.FileName}");
        }
    }

    private async void ShowPropertiesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var selectedTask = TaskListView.SelectedItem as TaskViewModel;
        if (selectedTask != null)
        {
            var properties = $"File: {selectedTask.FileName}\n" +
                           $"Full Path: {selectedTask.FilePath}\n" +
                           $"Duration: {selectedTask.Duration}\n" +
                           $"Model: {selectedTask.ModelName}\n" +
                           $"Language: {selectedTask.Language}\n" +
                           $"Status: {selectedTask.Status}\n" +
                           $"Progress: {selectedTask.Progress:F1}%";
            
            if (!string.IsNullOrEmpty(selectedTask.ErrorMessage))
            {
                properties += $"\nError: {selectedTask.ErrorMessage}";
            }
            
            if (!string.IsNullOrEmpty(selectedTask.OutputPath))
            {
                properties += $"\nOutput: {selectedTask.OutputPath}";
            }

            var dialog = new ContentDialog
            {
                Title = "Task Properties",
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = properties,
                        IsTextSelectionEnabled = true,
                        TextWrapping = TextWrapping.Wrap
                    }
                },
                CloseButtonText = "Close",
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
        }
    }

    // Keyboard accelerator handlers
    private void OpenFiles_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        AddFilesButton_Click(null, null);
        args.Handled = true;
    }

    private void OpenFolder_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        AddFolderButton_Click(null, null);
        args.Handled = true;
    }

    private void SaveQueue_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        SaveQueueButton_Click(null, null);
        args.Handled = true;
    }

    private void LoadQueue_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        LoadQueueButton_Click(null, null);
        args.Handled = true;
    }

    private void StartQueue_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        StartQueueButton_Click(null, null);
        args.Handled = true;
    }

    private void PauseQueue_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        PauseQueueButton_Click(null, null);
        args.Handled = true;
    }

    private void CancelQueue_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        CancelCurrentButton_Click(null, null);
        args.Handled = true;
    }

    private void DeleteTask_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (TaskListView.SelectedItem != null)
        {
            RemoveTaskMenuItem_Click(null, null);
        }
        args.Handled = true;
    }

    private void ShowHelp_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        AboutButton_Click(null, null);
        args.Handled = true;
    }

    // Drag and drop handlers
    private void TaskListView_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        
        if (e.DragUIOverride != null)
        {
            e.DragUIOverride.Caption = "Drop files to add to queue";
            e.DragUIOverride.IsGlyphVisible = true;
            e.DragUIOverride.IsContentVisible = true;
        }
    }

    private async void TaskListView_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                var filePaths = new List<string>();
                
                foreach (var item in items)
                {
                    if (item is StorageFile file)
                    {
                        filePaths.Add(file.Path);
                    }
                    else if (item is StorageFolder folder)
                    {
                        await ProcessDroppedFolderAsync(folder, filePaths);
                    }
                }
                
                if (filePaths.Count > 0)
                {
                    // Filter for supported file types
                    var supportedExtensions = new[] { ".mp3", ".wav", ".mp4", ".avi", ".mkv", ".m4a", ".flac", ".ogg", ".webm", ".wma" };
                    var supportedFiles = filePaths.Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLower())).ToList();
                    
                    if (supportedFiles.Count > 0)
                    {
                        await AddFilesToQueue(supportedFiles);
                        UpdateStatus($"Added {supportedFiles.Count} files via drag and drop");
                    }
                    else
                    {
                        UpdateStatus("No supported audio/video files found in dropped items");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error processing dropped files: {ex.Message}");
        }
    }

    private async Task ProcessDroppedFolderAsync(StorageFolder folder, List<string> filePaths)
    {
        try
        {
            var files = await folder.GetFilesAsync();
            foreach (var file in files)
            {
                filePaths.Add(file.Path);
            }
            
            // Recursively process subfolders if recursive is enabled
            if (RecursiveCheckBox.IsChecked == true)
            {
                var subfolders = await folder.GetFoldersAsync();
                foreach (var subfolder in subfolders)
                {
                    await ProcessDroppedFolderAsync(subfolder, filePaths);
                }
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error processing folder {folder.Name}: {ex.Message}");
        }
    }

    private void InitializeSystemTray()
    {
        // Simplified system tray initialization
        _systemTrayService.ShowWindowRequested += OnSystemTrayShowWindow;
        _systemTrayService.HideWindowRequested += OnSystemTrayHideWindow;
    }

    private void OnSystemTrayShowWindow(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            App.MainWindow.Activate();
        });
    }

    private void OnSystemTrayHideWindow(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            App.MainWindow.AppWindow.Hide();
        });
    }

    private void ShowSettingsPage()
    {
        UpdateStatus("Settings page not yet implemented");
    }


    public void HandleMinimizeToTray()
    {
        if (_settingsService.Settings.MinimizeToTray && _settingsService.Settings.SystemTrayEnabled)
        {
            App.MainWindow.AppWindow.Hide();
        }
    }

    public bool ShouldCloseToTray()
    {
        return _settingsService.Settings.CloseToTray && _settingsService.Settings.SystemTrayEnabled;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        // Show system tray notification on startup if enabled
        if (_settingsService.Settings.SystemTrayEnabled)
        {
            _systemTrayService.ShowNotification("Whisper Transcription Queue", "Application started successfully");
        }
    }

    private async Task OpenOutputFileAsync(string outputPath)
    {
        try
        {
            var storageFile = await StorageFile.GetFileFromPathAsync(outputPath);
            await Windows.System.Launcher.LaunchFileAsync(storageFile);
        }
        catch (Exception ex)
        {
            UpdateStatus($"Failed to open output file: {ex.Message}");
        }
    }
}