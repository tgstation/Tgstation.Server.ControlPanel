using Avalonia;

namespace Tgstation.Server.ControlPanel.Core
{
	class Program
	{
		static void Main() => ControlPanel.Run(new NoopUpdater());
		public static AppBuilder BuildAvaloniaApp() => ControlPanel.BuildAvaloniaApp();
	}
}
