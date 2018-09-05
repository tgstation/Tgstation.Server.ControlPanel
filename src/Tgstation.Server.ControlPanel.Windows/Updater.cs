using Squirrel;
using System;
using System.Threading.Tasks;

namespace Tgstation.Server.ControlPanel.Windows
{
	sealed class Updater : IUpdater
	{
		readonly Task<UpdateManager> updateManagerTask;

		public bool Functional
		{
			get
			{
				try
				{
					using (var mgr = new UpdateManager(null))
						return mgr.IsInstalledApp;
				}
				catch (Exception)
				{
					return false;
				}
			}
		}

		public Updater()
		{
			updateManagerTask = Functional ? UpdateManager.GitHubUpdateManager("https://github.com/tgstation/Tgstation.Server.ControlPanel") : null;
		}

		public void Dispose() => updateManagerTask?.GetAwaiter().GetResult().Dispose();

		public async Task ApplyUpdate(Action<int> progress)
		{
			var manager = await updateManagerTask.ConfigureAwait(false);
			await manager.UpdateApp(progress).ConfigureAwait(false);
		}

		public void RestartApp() => UpdateManager.RestartApp();

		public async Task<Version> LatestVersion(Action<int> progress)
		{
			var manager = await updateManagerTask.ConfigureAwait(false);
			UpdateInfo updateInfo;
			try
			{
				updateInfo = await manager.CheckForUpdate(progress: progress).ConfigureAwait(false);
			}
			catch (InvalidOperationException)
			{
				return null;
			}
			return updateInfo.FutureReleaseEntry?.Version.Version;
		}
	}
}
