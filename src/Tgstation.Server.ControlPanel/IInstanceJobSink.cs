using System;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;

namespace Tgstation.Server.ControlPanel
{
	public interface IInstanceJobSink
	{
		void RegisterJob(Job job, Func<CancellationToken, Task> onStopped = null);
	}
}
