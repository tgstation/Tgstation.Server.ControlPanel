using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

using Microsoft.AspNetCore.SignalR.Client;

using ReactiveUI;

using Tgstation.Server.Api.Hubs;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Models.Response;
using Tgstation.Server.Client;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	public sealed class ServerJobSinkViewModel : ViewModelBase, IServerJobSink, IAsyncDisposable, IJobsHub
	{
		public IReadOnlyList<JobViewModel> Jobs
		{
			get => jobs;
			set => this.RaiseAndSetIfChanged(ref jobs, value);
		}

		public string ServerName => nameProvider();

		readonly Func<Task<Tuple<IServerClient, ServerInformationResponse>>> clientProvider;
		readonly Func<TimeSpan> requeryRateProvider;
		readonly Func<string> nameProvider;

		readonly Dictionary<long, InstanceJobSink> instanceSinks;
		readonly CancellationTokenSource cancellationTokenSource;

		readonly Action onDisposed;

		readonly Dictionary<long, JobViewModel> jobModelMap;

		readonly Task updateTask;

		readonly Func<UserResponse> currentUserProvider;

		TaskCompletionSource<object> updated;

		IReadOnlyList<JobViewModel> jobs;

		IServerClient currentClient;

		public ServerJobSinkViewModel(Func<Task<Tuple<IServerClient, ServerInformationResponse>>> clientProvider, Func<TimeSpan> requeryRateProvider, Func<string> nameProvider, Func<UserResponse> currentUserProvider, Action onDisposed)
		{
			this.clientProvider = clientProvider ?? throw new ArgumentNullException(nameof(clientProvider));
			this.requeryRateProvider = requeryRateProvider ?? throw new ArgumentNullException(nameof(requeryRateProvider));
			this.nameProvider = nameProvider ?? throw new ArgumentNullException(nameof(nameProvider));
			this.currentUserProvider = currentUserProvider ?? throw new ArgumentNullException(nameof(currentUserProvider));
			this.onDisposed = onDisposed ?? throw new ArgumentNullException(nameof(onDisposed));

			instanceSinks = new Dictionary<long, InstanceJobSink>();
			jobs = new List<JobViewModel>();
			cancellationTokenSource = new CancellationTokenSource();
			jobModelMap = new Dictionary<long, JobViewModel>();
			updated = new TaskCompletionSource<object>();

			//avalonia does some fuckery with the async context so we need to detach this ourselves
			updateTask = Task.Run(UpdateLoop);
		}

		public async ValueTask DisposeAsync()
		{
			cancellationTokenSource.Cancel();
			await updateTask;
			cancellationTokenSource.Dispose();
			onDisposed();
			foreach (var I in instanceSinks)
				I.Value.Dispose();
		}

		public async Task<IInstanceJobSink> GetSinkForInstance(IInstanceClient instanceClient, CancellationToken cancellationToken)
		{
			InstanceJobSink sink;
			lock (instanceSinks)
			{
				var newSink = !instanceSinks.TryGetValue(instanceClient.Metadata.Id.Value, out sink);
				if (newSink)
				{
					sink = new InstanceJobSink(instanceClient.Metadata, currentUserProvider);
					instanceSinks.Add(instanceClient.Metadata.Id.Value, sink);
				}
			}

			if (instanceClient.Metadata.Online == true)
			{
				await sink.InitialQuery(instanceClient.Jobs, cancellationToken).ConfigureAwait(false);
				if (sink.Updated.IsCompleted)
					lock (this)
					{
						updated.SetResult(null);
						updated = new TaskCompletionSource<object>();
					}
			}
			return sink;
		}

		IObservable<JobResponse> UpdateJobs(IServerClient client, CancellationToken cancellationToken)
		{
			var observables = new List<IObservable<JobResponse>>();
			lock (instanceSinks)
				foreach (var I in instanceSinks)
					observables.Add(I.Value.UpdateJobs(client.Instances.CreateClient(I.Value.Instance).Jobs, cancellationToken));
			return observables.Merge();
		}

		long? DeregisterJob(JobResponse job, out Func<CancellationToken, Task> postAction)
		{
			lock (instanceSinks)
				foreach (var I in instanceSinks)
					if (I.Value.DeregisterJob(job, out postAction))
						return I.Key;
			postAction = null;
			return null;
		}

		async Task UpdateJobList(JobResponse job, IServerClient serverClient)
		{
			if (job == null)
				return;

			long? instanceId = null;
			Func<CancellationToken, Task> deregTaskInvoker = null;
			if (job.StoppedAt.HasValue)
				instanceId = DeregisterJob(job, out deregTaskInvoker);
			else
				lock (instanceSinks)
					foreach (var I in instanceSinks)
						if (I.Value.TracksJob(job))
							instanceId = I.Key;

			if (!instanceId.HasValue)
			{
				instanceId = job.InstanceId;

				if (!instanceId.HasValue)
					return;
			}

			var client = serverClient.Instances.CreateClient(new InstanceResponse
			{
				Id = instanceId.Value
			}).Jobs;

			JobViewModel viewModel;
			lock (jobModelMap)
				if (!jobModelMap.TryGetValue(job.Id.Value, out viewModel))
				{
					JobViewModel newModel = null;
					newModel = new JobViewModel(job, () =>
					{
						DeregisterJob(job, out var innerDeregTask);
						lock (jobModelMap)
						{
							jobModelMap.Remove(job.Id.Value);
							Jobs = new List<JobViewModel>(Jobs.Where(x => x != newModel));
						}
					}, client);
					jobModelMap.Add(job.Id.Value, newModel);
					Jobs = new List<JobViewModel>(Jobs)
					{
						newModel
					};
					return;
				}
			await Dispatcher.UIThread.InvokeAsync(async () =>
			{
				viewModel.Update(job, client);
				if (deregTaskInvoker != null)
					await deregTaskInvoker(default).ConfigureAwait(true);
			}).ConfigureAwait(false);
		}

		IObservable<JobResponse> NewJobs()
		{
			var observables = new List<IObservable<JobResponse>>();
			lock (instanceSinks)
				foreach (var I in instanceSinks)
					observables.Add(I.Value.NewJobs());
			return observables.Merge();
		}

		async Task UpdateLoop()
		{
			var cancellationToken = cancellationTokenSource.Token;

			Task connectionLifetime = Task.CompletedTask;
			IAsyncDisposable currentConnection = null;
			Task<Tuple<IServerClient, ServerInformationResponse>> newestServerClient = clientProvider();
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					var (localCurrentClient, serverInfo) = await newestServerClient.WaitAsync(cancellationToken);
					currentClient = localCurrentClient;

					if (currentConnection != null)
						await currentConnection.DisposeAsync();

					if (serverInfo.ApiVersion.Major < 9 || (serverInfo.ApiVersion.Major == 9 && serverInfo.ApiVersion.Minor < 13))
					{
						newestServerClient = LegacyLoop(currentClient);
					}
					else
					{
						currentConnection = await currentClient.SubscribeToJobUpdates(
							this,
							cancellationToken: cancellationToken);
						newestServerClient = clientProvider();
					}
				}
				catch { }
			}

			if (currentConnection != null)
				await currentConnection.DisposeAsync();
		}

		async Task<Tuple<IServerClient, ServerInformationResponse>> LegacyLoop(IServerClient serverClient)
		{
			var cancellationToken = cancellationTokenSource.Token;

			Task<Tuple<IServerClient, ServerInformationResponse>> newestServerClient = clientProvider();
			while (!newestServerClient.IsCompleted && !cancellationToken.IsCancellationRequested)
			{
				Task oldDelayTask = null;
				try
				{
					var delay = oldDelayTask ?? Task.Delay(requeryRateProvider(), cancellationToken);
					this.RaisePropertyChanged(nameof(ServerName));
					Task updates, instanceUpdates;
					lock (this)
						updates = updated.Task;

					lock (instanceSinks)
					{
						var tasks = new List<Task>();
						foreach (var I in instanceSinks)
							tasks.Add(I.Value.Updated);
						instanceUpdates = tasks.Count > 0 ? (Task)Task.WhenAny(tasks) : new TaskCompletionSource<object>().Task;
					}

					await Task.WhenAny(updates, instanceUpdates, delay, newestServerClient).ConfigureAwait(false);
					if (newestServerClient.IsCompleted)
						break;

					var timedOut = delay.IsCompleted;
					if (!timedOut)
						oldDelayTask = delay;
					else
						oldDelayTask = null;
					if (serverClient != null)
					{
						var jobUpdates = timedOut ? UpdateJobs(serverClient, cancellationToken) : NewJobs();

						var tasks = new List<Task>();
						await jobUpdates.ForEachAsync(job =>
						{
							lock (tasks)
								tasks.Add(UpdateJobList(job, serverClient));
						}).ConfigureAwait(false);

						await Task.WhenAll(tasks).ConfigureAwait(false);
					}
				}
				catch { }
			}

			if (cancellationToken.IsCancellationRequested)
				return null;

			return await newestServerClient;
		}

		public void NameUpdate() => this.RaisePropertyChanged(nameof(ServerName));

		public Task ReceiveJobUpdate(JobResponse job, CancellationToken cancellationToken)
		{
			NewJobs(); // prevent the sinks from filling up
			return UpdateJobList(job, currentClient);
		}
	}
}
