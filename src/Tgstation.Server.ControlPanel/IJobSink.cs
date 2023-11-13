using System;
using System.Threading.Tasks;

using Tgstation.Server.Api.Models.Response;
using Tgstation.Server.Client;

namespace Tgstation.Server.ControlPanel
{
	public interface IJobSink
	{
		IServerJobSink GetServerSink(Func<Task<Tuple<IServerClient, ServerInformationResponse>>> clientProvider, Func<TimeSpan> timeSpanProvider, Func<string> nameProvider, Func<UserResponse> getCurrentUser);
	}
}
