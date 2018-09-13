using Avalonia;
using System.Web;

namespace Tgstation.Server.ControlPanel.Windows
{
	class Program : IUrlEncoder
	{
		public static void Main() => ControlPanel.Run(new Program(), new Updater());
		public static AppBuilder BuildAvaloniaApp() => ControlPanel.BuildAvaloniaApp();

		public string UrlEncode(string input) => HttpUtility.UrlEncode(input);
	}
}
