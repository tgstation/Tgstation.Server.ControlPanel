using System;
using Tgstation.Server.Client;

namespace Tgstation.Server.ControlPanel
{
	public interface IJobSink
	{
		IServerJobSink GetServerSink(Func<IServerClient> clientProvider, Func<TimeSpan> timespanProvider, Func<string> serverNameProvider);
	}
}
