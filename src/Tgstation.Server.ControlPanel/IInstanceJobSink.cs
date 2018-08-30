using Tgstation.Server.Api.Models;

namespace Tgstation.Server.ControlPanel
{
	public interface IInstanceJobSink
	{
		void RegisterJob(Job job);
	}
}
