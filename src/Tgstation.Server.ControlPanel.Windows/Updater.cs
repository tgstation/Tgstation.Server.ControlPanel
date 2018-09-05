using Squirrel;
using System;
using System.Threading.Tasks;

namespace Tgstation.Server.ControlPanel.Windows
{
	sealed class Updater : IUpdater
	{
		readonly Task<UpdateManager> updateManagerTask;

		public bool Functional => true;

		public Updater()
		{
			updateManagerTask = UpdateManager.GitHubUpdateManager("https://github.com/tgstation/Tgstation.Server.ControlPanel");
		}

		public void Dispose() => updateManagerTask.GetAwaiter().GetResult().Dispose();

		public async Task ApplyUpdate(Action<int> progress)
		{
			var manager = await updateManagerTask.ConfigureAwait(false);
			await manager.UpdateApp(progress).ConfigureAwait(false);
		}

		public void RestartApp() => UpdateManager.RestartApp();

		public async Task<Version> LatestVersion(Action<int> progress)
		{
			var manager = await updateManagerTask.ConfigureAwait(false);
			var updateInfo = await manager.CheckForUpdate(progress: progress).ConfigureAwait(false);
			return updateInfo.FutureReleaseEntry?.Version.Version;
		}
	}
}
