using System.Diagnostics;

namespace Tgstation.Server.ControlPanel
{
	public static class ControlPanel
	{
		public static void OpenFolder(string folder) => Process.Start(folder);
		public static Process LaunchUrl(string url, bool dispose = true)
		{
			Process result = null;
			try
			{
				result = Process.Start(new ProcessStartInfo
				{
					FileName = url,
					UseShellExecute = true
				});
			}
			catch
			{
				try
				{
					result = Process.Start("xdg-open", url);
				}
				catch { }
			}

			if (dispose)
				result?.Dispose();

			return result;
		}
	}
}
