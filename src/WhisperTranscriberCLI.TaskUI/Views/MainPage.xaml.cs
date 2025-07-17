using Microsoft.UI.Xaml;
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
using WhisperTranscriberCLI.TaskUI.ViewModels;

namespace WhisperTranscriberCLI.TaskUI.Views;

public sealed partial class MainPage : Page
{
    private readonly ObservableCollection<TaskViewModel> _tasks = new();
    private readonly ModelDiscovery _modelDiscovery;
    private readonly AudioDurationService _audioDurationService;
    private QueueManager? _queueManager;
    
    public MainPage()
    {
        this.InitializeComponent();
        _modelDiscovery = new ModelDiscovery();
        _audioDurationService = new AudioDurationService();
        
        TaskListView.ItemsSource = _tasks;
        LoadModels();
        InitializeQueueManager();
    }

    private void LoadModels()
    {
        var models = _modelDiscovery.GetAvailableModels();
        ModelComboBox.ItemsSource = models;
        ModelComboBox.DisplayMemberPath = "Name";
        ModelComboBox.SelectedValuePath = "Name";
        
        if (models.Count > 0)
        {
            ModelComboBox.SelectedIndex = 0;
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
        }
    }

    private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Model selection changed
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateStatus("Settings not yet implemented");
    }

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateStatus("About: Whisper Transcription Queue UI v1.0");
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
        });
    }

    private void OnTaskFailed(object? sender, TranscriptionTask e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateStatus($"Task failed: {Path.GetFileName(e.FilePath)} - {e.ErrorMessage}");
        });
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
}