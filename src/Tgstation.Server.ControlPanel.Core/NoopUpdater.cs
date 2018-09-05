using System;
using System.Threading.Tasks;

namespace Tgstation.Server.ControlPanel.Core
{
	sealed class NoopUpdater : IUpdater
	{
		public bool Functional => false;

		public Task ApplyUpdate(Action<int> progress) => throw new NotSupportedException();

		public void Dispose() { }

		public Task<Version> LatestVersion(Action<int> progress) => throw new NotSupportedException();

		public void RestartApp() => throw new NotSupportedException();
	}
}