using System;
using System.Threading.Tasks;

namespace Tgstation.Server.ControlPanel
{
	sealed class NoopUpdater : IUpdater
	{
		public bool Functional => false;

		public bool CanRestart => false;

		public Task ApplyUpdate(Action<int> progress) => throw new NotSupportedException();

		public void Dispose() { }

		public Task<Version> LatestVersion(Action<int> progress) => throw new NotSupportedException();

		public void RestartApp() => throw new NotSupportedException();
	}
}