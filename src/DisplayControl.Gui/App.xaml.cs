using Microsoft.UI.Xaml;

namespace DisplayControl.Gui
{
    /// <summary>
    /// Application entry point for the DisplayControl WinUI 3 desktop app.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;

        /// <summary>
        /// Occurs when the application is launched by the end user.
        /// </summary>
        /// <param name="args">Launch arguments.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
        }
    }
}

