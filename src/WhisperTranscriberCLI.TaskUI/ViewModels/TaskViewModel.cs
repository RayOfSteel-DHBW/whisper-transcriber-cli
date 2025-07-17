using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using WhisperTranscriberCLI.Core.Models;

namespace WhisperTranscriberCLI.TaskUI.ViewModels;

public class TaskViewModel : INotifyPropertyChanged
{
    private readonly TranscriptionTask _task;
    private string _status;
    private double _progress;
    private string? _errorMessage;
    private string? _outputPath;

    public TaskViewModel(TranscriptionTask task)
    {
        _task = task;
        _status = task.Status.ToString();
        _progress = task.Progress;
        _errorMessage = task.ErrorMessage;
        _outputPath = task.OutputPath;
    }

    public string Id => _task.Id;
    public string FilePath => _task.FilePath;
    public string FileName => Path.GetFileName(_task.FilePath);
    public string ModelName => _task.ModelName;
    public string Language => _task.Language;
    public string Duration => _task.Duration;

    public string Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
            }
        }
    }

    public double Progress
    {
        get => _progress;
        set
        {
            if (_progress != value)
            {
                _progress = value;
                OnPropertyChanged();
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage != value)
            {
                _errorMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public string? OutputPath
    {
        get => _outputPath;
        set
        {
            if (_outputPath != value)
            {
                _outputPath = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}