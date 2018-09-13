using Avalonia;
using Avalonia.Logging.Serilog;
using System.Diagnostics;
using Tgstation.Server.ControlPanel.ViewModels;
using Tgstation.Server.ControlPanel.Views;

namespace Tgstation.Server.ControlPanel
{
	public static class ControlPanel
	{
		public static void Run(IUrlEncoder urlEncoder, IUpdater updater)
		{
			using (var mwvm = new MainWindowViewModel(urlEncoder, updater))
			{
				var app = BuildAvaloniaApp();

				app.BeforeStarting(x => mwvm.AsyncStart());
				app.Start<MainWindow>(() => mwvm);
			}
		}

		public static AppBuilder BuildAvaloniaApp()
			=> AppBuilder.Configure<App>()
				.UsePlatformDetect()
				.UseReactiveUI()
				.LogToDebug();

		public static void LaunchUrl(string url)
		{
			try
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = url,
					UseShellExecute = true
				}).Dispose();
			}
			catch
			{
				try
				{
					Process.Start("xdg-open", url).Dispose();
				}
				catch { }
			}
		}
	}
}
