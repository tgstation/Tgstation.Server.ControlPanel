using Avalonia;

namespace Tgstation.Server.ControlPanel.Windows
{
	static class Program
	{
		public static void Main() => ControlPanel.Run(new Updater());
		public static AppBuilder BuildAvaloniaApp() => ControlPanel.BuildAvaloniaApp();
	}
}
