using Avalonia;
using System.Net;
using System.Web;

namespace Tgstation.Server.ControlPanel.Windows
{
	class Program : IUrlEncoder
	{
		public static void Main() => ControlPanel.Run(new Program(), new NoopUpdater());
		public static AppBuilder BuildAvaloniaApp()
		{
			// Add Tls1.2 to the existing enabled protocols
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

			return ControlPanel.BuildAvaloniaApp();
		}

		public string UrlEncode(string input) => HttpUtility.UrlEncode(input);
	}
}
