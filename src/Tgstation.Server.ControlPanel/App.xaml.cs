using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Net;
using Tgstation.Server.ControlPanel.ViewModels;
using Tgstation.Server.ControlPanel.Views;

namespace Tgstation.Server.ControlPanel
{
    public sealed class App : Application
    {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            // Add Tls1.2 to the existing enabled protocols
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            using var mwvm = new MainWindowViewModel(new NotificationUpdater());
            mwvm.AsyncStart();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                desktop.MainWindow = new MainWindow()
                {
                    DataContext = mwvm
                };
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            {
                singleView.MainView = new MainView()
                {
                    DataContext = mwvm
                };
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
