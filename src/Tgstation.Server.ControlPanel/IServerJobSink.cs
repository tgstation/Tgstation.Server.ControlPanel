using System;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel
{
	public interface IServerJobSink : IDisposable
	{
		Task<IInstanceJobSink> GetSinkForInstance(IInstanceClient instanceClient, CancellationToken cancellationToken);
	}
}
