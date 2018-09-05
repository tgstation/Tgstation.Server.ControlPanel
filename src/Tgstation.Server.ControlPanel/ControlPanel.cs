using Avalonia;
using Avalonia.Logging.Serilog;
using Tgstation.Server.ControlPanel.ViewModels;
using Tgstation.Server.ControlPanel.Views;

namespace Tgstation.Server.ControlPanel
{
	public static class ControlPanel
	{
		public static void Run(IUpdater updater)
		{
			using (var mwvm = new MainWindowViewModel(updater))
				BuildAvaloniaApp().Start<MainWindow>(() => mwvm);
		}

		static AppBuilder BuildAvaloniaApp()
			=> AppBuilder.Configure<App>()
				.UsePlatformDetect()
				.UseReactiveUI()
				.LogToDebug();
	}
}
