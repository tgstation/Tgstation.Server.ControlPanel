using System;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Client;

namespace Tgstation.Server.ControlPanel
{
	public interface IJobSink
	{
		IServerJobSink GetServerSink(Func<IServerClient> clientProvider, Func<TimeSpan> timeSpanProvider, Func<string> nameProvider, Func<User> getCurrentUser);
	}
}
