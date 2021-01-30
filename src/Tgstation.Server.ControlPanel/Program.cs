using System.Linq;
using Avalonia;
using Avalonia.ReactiveUI;

namespace Tgstation.Server.ControlPanel
{
	static class Program
	{
		public static void Main(string[] args) => BuildAvaloniaApp(args).StartWithClassicDesktopLifetime(args);

		public static AppBuilder BuildAvaloniaApp(string[] args)
		{
			AppBuilder app = AppBuilder.Configure<App>();
			if (args.FirstOrDefault()?.ToUpperInvariant() == "--WIN32-HACK")
				app
					.UseWin32()
					.UseReactiveUI()
					.LogToTrace();
			else
				app
					.UsePlatformDetect()
					.UseReactiveUI()
					.LogToTrace();

			return app;
		}
	}
}
