using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveUI;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Client;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	public sealed class JobManagerViewModel : ViewModelBase, IJobSink, IDisposable
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

		public void Dispose()
		{
			while (jobSinks.Count > 0)
				jobSinks[0].Dispose();
		}

		public IServerJobSink GetServerSink(Func<IServerClient> clientProvider, Func<TimeSpan> timeSpanProvider, Func<string> nameProvider, Func<User> getCurrentUser)
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
