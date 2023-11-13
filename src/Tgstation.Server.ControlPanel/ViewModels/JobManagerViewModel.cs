using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ReactiveUI;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Models.Response;
using Tgstation.Server.Client;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	public sealed class JobManagerViewModel : ViewModelBase, IJobSink, IAsyncDisposable
	{
		public List<ServerJobSinkViewModel> Sinks
		{
			get => jobSinks;
			set => this.RaiseAndSetIfChanged(ref jobSinks, value);
		}

		List<ServerJobSinkViewModel> jobSinks;

		public JobManagerViewModel()
		{
			Sinks = new List<ServerJobSinkViewModel>();
		}

		public async ValueTask DisposeAsync()
		{
			while (jobSinks.Count > 0)
				await jobSinks[0].DisposeAsync();
		}

		public IServerJobSink GetServerSink(Func<Task<Tuple<IServerClient, ServerInformationResponse>>> clientProvider, Func<TimeSpan> timeSpanProvider, Func<string> nameProvider, Func<UserResponse> getCurrentUser)
		{
			ServerJobSinkViewModel sink = null;
			sink = new ServerJobSinkViewModel(clientProvider, timeSpanProvider, nameProvider, getCurrentUser, () =>
			{
				lock (this)
					Sinks = new List<ServerJobSinkViewModel>(jobSinks.Where(x => x != sink));
			});
			lock (this)
				Sinks = new List<ServerJobSinkViewModel>(jobSinks)
				{
					sink
				};
			return sink;
		}
	}
}
