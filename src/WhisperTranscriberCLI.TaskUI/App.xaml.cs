using Microsoft.UI.Xaml.Navigation;
using WhisperTranscriberCLI.TaskUI.Views;

namespace WhisperTranscriberCLI.TaskUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? window;
        private MainPage? mainPage;
        
        public static Window MainWindow { get; private set; } = null!;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            window ??= new Window();
            MainWindow = window;

            if (window.Content is not Frame rootFrame)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                window.Content = rootFrame;
            }

            _ = rootFrame.Navigate(typeof(MainPage), e.Arguments);
            window.Title = "Whisper Transcription Queue";
            
            // Set up window closing behavior
            window.Closed += OnWindowClosed;
            
            window.Activate();
        }
        
        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            // Get the main page to check close-to-tray setting
            if (window?.Content is Frame frame && frame.Content is MainPage mainPageInstance)
            {
                if (mainPageInstance.ShouldCloseToTray())
                {
                    // Cancel the close and hide instead
                    args.Handled = true;
                    window.Hide();
                    return;
                }
            }
            
            // Normal close behavior
            this.Exit();
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }
    }
}
