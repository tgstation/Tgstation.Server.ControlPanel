using Avalonia;
using Avalonia.Logging.Serilog;
using Tgstation.Server.ControlPanel.ViewModels;
using Tgstation.Server.ControlPanel.Views;

namespace Tgstation.Server.ControlPanel
{
	static class Program
	{
		static void Main()
		{
			using (var mwvm = new MainWindowViewModel())
				BuildAvaloniaApp().Start<MainWindow>(() => mwvm);
		}

		public static AppBuilder BuildAvaloniaApp()
			=> AppBuilder.Configure<App>()
				.UsePlatformDetect()
				.UseReactiveUI()
				.LogToDebug();
	}
}
