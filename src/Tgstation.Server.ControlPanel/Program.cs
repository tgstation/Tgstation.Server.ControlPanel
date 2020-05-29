using Avalonia;
using System.Net;
using System.Web;

namespace Tgstation.Server.ControlPanel
{
	static class Program
	{
		public static void Main(string[] args) => ControlPanel.Run(new NoopUpdater(), args);
	}
}
