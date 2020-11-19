using Avalonia;
using Avalonia.Logging.Serilog;
using Avalonia.ReactiveUI;
using System.Linq;

namespace Tgstation.Server.ControlPanel
{
	static class Program
	{
		public static void Main(string[] args) => ControlPanel.Run(new NotificationUpdater(), BuildAvaloniaApp(args));

		public static AppBuilder BuildAvaloniaApp(string[] args)
		{
			AppBuilder app = AppBuilder.Configure<App>();
			if (args.FirstOrDefault()?.ToUpperInvariant() == "--WIN32-HACK")
				app
					.UseWin32()
					.UseDirect2D1()
					.UseReactiveUI()
					.LogToDebug();
			else
				app
					.UsePlatformDetect()
					.UseReactiveUI()
					.LogToDebug();

			return app;
		}
	}
}
