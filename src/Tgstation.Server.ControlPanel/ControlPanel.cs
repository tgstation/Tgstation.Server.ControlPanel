using Avalonia;
using Avalonia.Logging.Serilog;
using Avalonia.ReactiveUI;
using System.Diagnostics;
using System.Linq;
using System.Net;
using Tgstation.Server.ControlPanel.ViewModels;
using Tgstation.Server.ControlPanel.Views;

namespace Tgstation.Server.ControlPanel
{
	public static class ControlPanel
	{
		public static void Run(IUpdater updater, AppBuilder app)
		{
			// Add Tls1.2 to the existing enabled protocols
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

			using var mwvm = new MainWindowViewModel(updater);

			mwvm.AsyncStart();
			app.Start<MainWindow>(() => mwvm);
		}

		public static void OpenFolder(string folder) => Process.Start(folder);
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
