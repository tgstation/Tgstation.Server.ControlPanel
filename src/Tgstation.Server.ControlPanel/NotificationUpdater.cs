using Octokit;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Tgstation.Server.ControlPanel
{
	sealed class NotificationUpdater : IUpdater
	{
		public bool Functional => true;

		public bool CanRestart => false;

		public Task ApplyUpdate(Action<int> progress)
		{
			ControlPanel.LaunchUrl("https://github.com/tgstation/Tgstation.Server.ControlPanel/releases/latest");
			return Task.CompletedTask;
		}

		public void Dispose()
		{
		}

		public async Task<Version> LatestVersion(Action<int> progress)
		{
			var assemblyName = Assembly.GetExecutingAssembly().GetName();
			var productHeaderValue = new ProductHeaderValue(assemblyName.Name, assemblyName.Version.ToString());
			var client = new GitHubClient(productHeaderValue);
			var release = await client.Repository.Release.GetLatest("tgstation", "Tgstation.Server.ControlPanel");
			var versionString = release.TagName.Substring("Tgstation.Server.ControlPanel-v".Length) + ".0";
			if (Version.TryParse(versionString, out var version))
				return version;
			return null;
		}

		public void RestartApp() => throw new NotSupportedException();
	}
}
