using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Client;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	public sealed class ServerJobSinkViewModel : ViewModelBase, IServerJobSink, IDisposable
	{
		public IReadOnlyList<JobViewModel> Jobs
		{
			get => jobs;
			set => this.RaiseAndSetIfChanged(ref jobs, value);
		}

		public string ServerName => nameProvider();

		readonly Func<IServerClient> clientProvider;
		readonly Func<string> nameProvider;
		readonly JobManagerViewModel jobManagerViewModel;

		readonly Dictionary<long, InstanceJobSink> instanceSinks;

		readonly Action onDisposed;

		IReadOnlyList<JobViewModel> jobs;

		public ServerJobSinkViewModel(Func<IServerClient> clientProvider, Func<string> nameProvider, JobManagerViewModel jobManagerViewModel, Action onDisposed)
		{
			this.clientProvider = clientProvider ?? throw new ArgumentNullException(nameof(clientProvider));
			this.nameProvider = nameProvider ?? throw new ArgumentNullException(nameof(nameProvider));
			this.jobManagerViewModel = jobManagerViewModel ?? throw new ArgumentNullException(nameof(jobManagerViewModel));
			this.onDisposed = onDisposed ?? throw new ArgumentNullException(nameof(onDisposed));

			instanceSinks = new Dictionary<long, InstanceJobSink>();
			jobs = new List<JobViewModel> {
				new JobViewModel(new Job{
					Id = 1,
					StartedAt = DateTimeOffset.Now - TimeSpan.FromSeconds(50),
					StartedBy = new User
					{
						Name = "Some schmuck",
						Id= 42
					},
					Description = "In progress job"
				}, true),
				new JobViewModel(new Job{
					Id = 2,
					StartedAt = DateTimeOffset.Now,
					StoppedAt = DateTimeOffset.Now,
					StartedBy = new User
					{
						Name = "Some guy",
						Id= 69
					},
					Description = "Done job"
				}, true),
				new JobViewModel(new Job{
					Id = 3,
					StartedAt = DateTimeOffset.Now - TimeSpan.FromSeconds(20),
					StartedBy = new User
					{
						Name = "Some schmuck",
						Id= 42
					},
					Progress = 40,
					Description = "In progress job"
				}, false),
				new JobViewModel(new Job{
					Id = 4,
					StartedAt = DateTimeOffset.Now - TimeSpan.FromSeconds(20),
					StoppedAt = DateTimeOffset.Now,
					StartedBy = new User
					{
						Name = "Some schmuck",
						Id= 42
					},
					Description = "Errored job",
					ExceptionDetails = "Shit's fucked yo..."
				}, true),
				new JobViewModel(new Job{
					Id = 5,
					StartedAt = DateTimeOffset.Now - TimeSpan.FromSeconds(20),
					StoppedAt = DateTimeOffset.Now,
					StartedBy = new User
					{
						Name = "Some schmuck",
						Id= 42
					},
					Progress = 69,
					Description = "Cancelled job",
					Cancelled = true
				}, true)
			};
		}

		public void Dispose()
		{
			onDisposed();
			foreach (var I in instanceSinks)
				I.Value.Dispose();
		}

		public async Task<IInstanceJobSink> GetSinkForInstance(IInstanceClient instanceClient, CancellationToken cancellationToken)
		{
			bool newSink;
			InstanceJobSink sink;
			lock (instanceSinks)
			{
				newSink = !instanceSinks.TryGetValue(instanceClient.Metadata.Id, out sink);
				if (newSink)
				{
					sink = new InstanceJobSink(instanceClient.Metadata, jobManagerViewModel);
					instanceSinks.Add(instanceClient.Metadata.Id, sink);
				}
			}
			if (newSink)
				await sink.InitialQuery(instanceClient.Jobs, cancellationToken).ConfigureAwait(false);
			return sink;
		}

		public IObservable<Job> UpdateJobs(CancellationToken cancellationToken)
		{
			var client = clientProvider();
			if (client == null)
				return Observable.Empty<Job>();
			var observables = new List<IObservable<Job>>();
			lock (instanceSinks)
				foreach (var I in instanceSinks)
					observables.Add(I.Value.UpdateJobs(client.Instances.CreateClient(I.Value.Instance).Jobs, cancellationToken));
			return observables.Merge();
		}

		public void NameUpdate() => this.RaisePropertyChanged(nameof(ServerName));
	}
}
