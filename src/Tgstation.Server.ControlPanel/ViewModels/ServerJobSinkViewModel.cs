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
		readonly Func<TimeSpan> requeryRateProvider;
		readonly Func<string> nameProvider;
		readonly JobManagerViewModel jobManagerViewModel;

		readonly Dictionary<long, InstanceJobSink> instanceSinks;
		readonly CancellationTokenSource cancellationTokenSource;

		readonly Action onDisposed;

		readonly Dictionary<long, JobViewModel> jobModelMap;

		IReadOnlyList<JobViewModel> jobs;

		public ServerJobSinkViewModel(Func<IServerClient> clientProvider, Func<TimeSpan> requeryRateProvider, Func<string> nameProvider, JobManagerViewModel jobManagerViewModel, Action onDisposed)
		{
			this.clientProvider = clientProvider ?? throw new ArgumentNullException(nameof(clientProvider));
			this.requeryRateProvider = requeryRateProvider ?? throw new ArgumentNullException(nameof(requeryRateProvider));
			this.nameProvider = nameProvider ?? throw new ArgumentNullException(nameof(nameProvider));
			this.jobManagerViewModel = jobManagerViewModel ?? throw new ArgumentNullException(nameof(jobManagerViewModel));
			this.onDisposed = onDisposed ?? throw new ArgumentNullException(nameof(onDisposed));

			instanceSinks = new Dictionary<long, InstanceJobSink>();
			jobs = new List<JobViewModel>();
			cancellationTokenSource = new CancellationTokenSource();
			jobModelMap = new Dictionary<long, JobViewModel>();

			UpdateLoop();
		}

		public void Dispose()
		{
			cancellationTokenSource.Cancel();
			cancellationTokenSource.Dispose();
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

		long? DeregisterJob(Job job)
		{
			lock (instanceSinks)
				foreach (var I in instanceSinks)
					if (I.Value.DeregisterJob(job))
						return I.Key;
			return null;
		}

		async void UpdateLoop()
		{
			var cancellationToken = cancellationTokenSource.Token;
			while (!cancellationToken.IsCancellationRequested)
				try
				{
					var serverClient = clientProvider();
					var jobUpdates = UpdateJobs(cancellationToken);

					await jobUpdates.ForEachAsync(job =>
					{
						if (job == null)
							return;

						long? instanceId = null;
						if (job.StoppedAt.HasValue)
							instanceId = DeregisterJob(job);
						else
							lock (instanceSinks)
								foreach (var I in instanceSinks)
									if (I.Value.TracksJob(job))
										instanceId = I.Key;

						if (!instanceId.HasValue)
							return;

						var client = serverClient.Instances.CreateClient(new Instance
						{
							Id = instanceId.Value
						}).Jobs;

						lock (jobModelMap)
							if (!jobModelMap.TryGetValue(job.Id, out var viewModel))
								jobModelMap.Add(job.Id, new JobViewModel(job, () => DeregisterJob(job), client));
							else
								viewModel.Update(job, client);

					}).ConfigureAwait(false);

					await Task.Delay(requeryRateProvider(), cancellationToken).ConfigureAwait(false);
				}
				catch { }

		}

		public void NameUpdate() => this.RaisePropertyChanged(nameof(ServerName));
	}
}
