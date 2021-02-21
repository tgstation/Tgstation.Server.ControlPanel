using System;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models.Response;

namespace Tgstation.Server.ControlPanel
{
	public interface IInstanceJobSink
	{
		void RegisterJob(JobResponse job, Func<CancellationToken, Task> onStopped = null);
	}
}
