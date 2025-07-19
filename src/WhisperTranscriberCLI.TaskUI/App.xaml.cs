using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WhisperTranscriberCLI.TaskUI.Views;
using WhisperTranscriberCLI.TaskUI.Services;
using WhisperTranscriberCLI.Core.Services;

namespace WhisperTranscriberCLI.TaskUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? window;
        private IHost? _host;
        
        public static Window MainWindow { get; private set; } = null!;
        public static IServiceProvider Services { get; private set; } = null!;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
            
#if DEBUG
            // Enable immediate debug output flushing
            System.Diagnostics.Debug.AutoFlush = true;
#endif

            // Setup dependency injection and logging
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddFileLogging();
                    
                    // Register Core services (order matters!)
                    services.AddSingleton<UserSettingsService>();
                    services.AddSingleton<ModelDiscovery>();
                    services.AddSingleton<AudioDurationService>();
                    services.AddSingleton<SystemCheckService>();
                    
                    // Register TaskUI services
                    services.AddSingleton<SettingsService>();
                    services.AddSingleton<GpuMonitoringService>();
                    // ModelSetupService removed - requires Window which isn't available at DI build time
                    
                    // Register MainPage with DI
                    services.AddTransient<MainPage>();
                })
                .Build();
            
            Services = _host.Services;
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

            // Use dependency injection to get MainPage instance instead of navigating to the type
            var mainPage = Services.GetRequiredService<MainPage>();
            rootFrame.Content = mainPage;
            
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
                    window.AppWindow.Hide();
                    return;
                }
            }
            
            // Normal close behavior
            Exit();
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            var logger = Services.GetService<ILogger<App>>();
            logger?.LogError("Navigation failed to page: {PageType}", e.SourcePageType.FullName);
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }
    }
}
