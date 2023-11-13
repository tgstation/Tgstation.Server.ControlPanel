using System;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel
{
	public interface IServerJobSink : IAsyncDisposable
	{
		Task<IInstanceJobSink> GetSinkForInstance(IInstanceClient instanceClient, CancellationToken cancellationToken);
		void NameUpdate();
	}
}
