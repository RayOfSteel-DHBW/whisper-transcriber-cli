using Windows.Storage.Pickers;
using WinRT.Interop;
using Microsoft.UI.Xaml;
using WhisperTranscriberCLI.Core.Services;

namespace WhisperTranscriberCLI.TaskUI.Services;

public class ModelSetupService
{
    private readonly Window _parentWindow;

    public ModelSetupService(Window parentWindow)
    {
        _parentWindow = parentWindow;
    }

    public async Task<bool> ShowModelSetupDialogAsync()
    {
        var setupDialog = new ContentDialog
        {
            Title = "Whisper Models Setup",
            Content = CreateSetupContent(),
            PrimaryButtonText = "Browse",
            CloseButtonText = "Cancel",
            XamlRoot = _parentWindow.Content.XamlRoot
        };

        var result = await setupDialog.ShowAsync();
        
        if (result == ContentDialogResult.Primary)
        {
            var modelsPath = await BrowseForModelsDirectoryAsync();
            if (!string.IsNullOrEmpty(modelsPath))
            {
                var modelDiscovery = new ModelDiscovery(null);
                modelDiscovery.SetModelDirectory(modelsPath);
                return true;
            }
        }

        return false;
    }

    private async Task<string?> BrowseForModelsDirectoryAsync()
    {
        var picker = new FolderPicker();
        picker.ViewMode = PickerViewMode.Thumbnail;
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".bin");
        picker.FileTypeFilter.Add("*");

        var hwnd = WindowNative.GetWindowHandle(_parentWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder == null)
        {
            return null;
        }

        // Verify the folder contains model files
        var files = await folder.GetFilesAsync();
        var modelFiles = files.Where(f => f.FileType.Equals(".bin", StringComparison.OrdinalIgnoreCase)).ToList();

        if (modelFiles.Count == 0)
        {
            await ShowNoModelsFoundDialogAsync(folder.Path);
            return null;
        }

        return folder.Path;
    }

    private async Task ShowNoModelsFoundDialogAsync(string folderPath)
    {
        var errorDialog = new ContentDialog
        {
            Title = "No Models Found",
            Content = new TextBlock
            {
                Text = $"The selected folder does not contain any Whisper model files (.bin):\n\n{folderPath}\n\nPlease select a different folder or download models first.",
                TextWrapping = TextWrapping.Wrap
            },
            CloseButtonText = "OK",
            XamlRoot = _parentWindow.Content.XamlRoot
        };

        await errorDialog.ShowAsync();
    }

    private FrameworkElement CreateSetupContent()
    {
        var stackPanel = new StackPanel { Spacing = 12 };

        stackPanel.Children.Add(new TextBlock
        {
            Text = "No Whisper models found in the default locations.",
            TextWrapping = TextWrapping.Wrap
        });

        stackPanel.Children.Add(new TextBlock
        {
            Text = "Please select the folder containing your Whisper model files (.bin).",
            TextWrapping = TextWrapping.Wrap
        });

        stackPanel.Children.Add(new TextBlock
        {
            Text = "Download models from:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 4)
        });

        stackPanel.Children.Add(new HyperlinkButton
        {
            Content = "https://huggingface.co/ggerganov/whisper.cpp/tree/main",
            NavigateUri = new Uri("https://huggingface.co/ggerganov/whisper.cpp/tree/main")
        });

        stackPanel.Children.Add(new TextBlock
        {
            Text = "Recommended models: ggml-small.bin, ggml-base.bin, ggml-medium.bin",
            FontStyle = Windows.UI.Text.FontStyle.Italic,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        });

        return stackPanel;
    }
}