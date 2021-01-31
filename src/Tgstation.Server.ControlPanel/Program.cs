using System.Linq;
using Avalonia;
using Avalonia.ReactiveUI;

namespace Tgstation.Server.ControlPanel
{
	static class Program
	{
		/// <summary>
		/// Used for passing through to BuildAvaloniaApp, needed for the Win32-HACK's compatability with the Avalonia in-editor viewer
		/// </summary>
		private static string[] ApplicationArgs;

		public static void Main(string[] args)
		{
			ApplicationArgs = args;
			BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
		}

		public static AppBuilder BuildAvaloniaApp()
		{
			AppBuilder app = AppBuilder.Configure<App>();
			if (ApplicationArgs?.FirstOrDefault()?.ToUpperInvariant() == "--WIN32-HACK")
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
