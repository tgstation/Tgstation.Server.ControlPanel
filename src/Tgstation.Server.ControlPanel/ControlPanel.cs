using System.Diagnostics;

namespace Tgstation.Server.ControlPanel
{
	public static class ControlPanel
	{
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
