using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    private QueueManager? _queueManager;
    
    public MainPage()
    {
        this.InitializeComponent();
        _modelDiscovery = new ModelDiscovery();
        
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
            var task = new TranscriptionTask
            {
                FilePath = filePath,
                ModelName = selectedModel,
                Language = selectedLanguage,
                Duration = "00:00:00",
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
}