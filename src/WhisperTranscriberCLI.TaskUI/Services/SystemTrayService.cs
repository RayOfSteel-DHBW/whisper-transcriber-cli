using System;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;

namespace WhisperTranscriberCLI.TaskUI.Services;

public class SystemTrayService : IDisposable
{
    private readonly SettingsService _settingsService;
    private bool _isDisposed = false;

    public event EventHandler? ShowWindowRequested;
    public event EventHandler? HideWindowRequested;
    public event EventHandler? StartQueueRequested;
    public event EventHandler? PauseQueueRequested;
    public event EventHandler? ExitRequested;

    public SystemTrayService(Microsoft.UI.Xaml.Window mainWindow, SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public void ShowWindow()
    {
        ShowWindowRequested?.Invoke(this, EventArgs.Empty);
    }

    public void HideWindow()
    {
        HideWindowRequested?.Invoke(this, EventArgs.Empty);
    }

    public void ShowNotification(string title, string message, NotificationSeverity severity = NotificationSeverity.Information)
    {
        try
        {
            if (!_settingsService.Settings.SystemTrayEnabled)
                return;

            // Create toast notification
            var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
            var stringElements = toastXml.GetElementsByTagName("text");
            stringElements[0].AppendChild(toastXml.CreateTextNode(title));
            stringElements[1].AppendChild(toastXml.CreateTextNode(message));

            var toast = new ToastNotification(toastXml);
            ToastNotificationManager.CreateToastNotifier("Whisper Transcription Queue").Show(toast);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to show notification: {ex.Message}");
        }
    }

    public void UpdateToolTip(string tooltip)
    {
        // No-op for simplified version
    }

    public void SetEnabled(bool enabled)
    {
        // No-op for simplified version
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
    }
}

public enum NotificationSeverity
{
    Information,
    Warning,
    Error,
    Success
}