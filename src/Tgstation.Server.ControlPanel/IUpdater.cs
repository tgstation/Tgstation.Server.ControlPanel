using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tgstation.Server.ControlPanel
{
	public interface IUpdater : IDisposable
	{
		bool Functional { get; }

		Task<Version> LatestVersion(Action<int> progress);

		Task ApplyUpdate(Action<int> progress);

		void RestartApp();
	}
}
