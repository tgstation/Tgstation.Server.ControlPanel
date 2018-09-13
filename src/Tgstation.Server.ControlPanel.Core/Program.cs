using Avalonia;
using System.Web;

namespace Tgstation.Server.ControlPanel.Core
{
	class Program : IUrlEncoder
	{
		static void Main() => ControlPanel.Run(new Program(), new NoopUpdater());
		public static AppBuilder BuildAvaloniaApp() => ControlPanel.BuildAvaloniaApp();

		public string UrlEncode(string input) => HttpUtility.UrlEncode(input);
	}
}
