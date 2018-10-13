using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
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

		readonly Task updateTask;

		readonly Func<User> currentUserProvider;

		TaskCompletionSource<object> updated;

		IReadOnlyList<JobViewModel> jobs;

		public ServerJobSinkViewModel(Func<IServerClient> clientProvider, Func<TimeSpan> requeryRateProvider, Func<string> nameProvider, JobManagerViewModel jobManagerViewModel, Func<User> currentUserProvider, Action onDisposed)
		{
			this.clientProvider = clientProvider ?? throw new ArgumentNullException(nameof(clientProvider));
			this.requeryRateProvider = requeryRateProvider ?? throw new ArgumentNullException(nameof(requeryRateProvider));
			this.nameProvider = nameProvider ?? throw new ArgumentNullException(nameof(nameProvider));
			this.jobManagerViewModel = jobManagerViewModel ?? throw new ArgumentNullException(nameof(jobManagerViewModel));
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

		public void Dispose()
		{
			cancellationTokenSource.Cancel();
			updateTask.GetAwaiter().GetResult();
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
				var newSink = !instanceSinks.TryGetValue(instanceClient.Metadata.Id, out sink);
				if (newSink)
				{
					sink = new InstanceJobSink(instanceClient.Metadata, jobManagerViewModel, currentUserProvider);
					instanceSinks.Add(instanceClient.Metadata.Id, sink);
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

		public IObservable<Job> UpdateJobs(IServerClient client, CancellationToken cancellationToken)
		{
			var observables = new List<IObservable<Job>>();
			lock (instanceSinks)
				foreach (var I in instanceSinks)
					observables.Add(I.Value.UpdateJobs(client.Instances.CreateClient(I.Value.Instance).Jobs, cancellationToken));
			return observables.Merge();
		}

		long? DeregisterJob(Job job, out Func<CancellationToken, Task> postAction)
		{
			lock (instanceSinks)
				foreach (var I in instanceSinks)
					if (I.Value.DeregisterJob(job, out postAction))
						return I.Key;
			postAction = null;
			return null;
		}

		async Task UpdateJobList(Job job, IServerClient serverClient)
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
				return;

			var client = serverClient.Instances.CreateClient(new Instance
			{
				Id = instanceId.Value
			}).Jobs;

			JobViewModel viewModel;
			lock (jobModelMap)
				if (!jobModelMap.TryGetValue(job.Id, out viewModel))
				{
					JobViewModel newModel = null;
					newModel = new JobViewModel(job, () =>
					{
						DeregisterJob(job, out var innerDeregTask);
						lock (jobModelMap)
						{
							jobModelMap.Remove(job.Id);
							Jobs = new List<JobViewModel>(Jobs.Where(x => x != newModel));
						}
					}, client);
					jobModelMap.Add(job.Id, newModel);
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

		IObservable<Job> NewJobs()
		{
			var observables = new List<IObservable<Job>>();
			lock (instanceSinks)
				foreach (var I in instanceSinks)
					observables.Add(I.Value.NewJobs());
			return observables.Merge();
		}

		async Task UpdateLoop()
		{
			var cancellationToken = cancellationTokenSource.Token;

			while (!cancellationToken.IsCancellationRequested)
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

					await Task.WhenAny(updates, instanceUpdates, delay).ConfigureAwait(false);
					var serverClient = clientProvider();
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

		}

		public void NameUpdate() => this.RaisePropertyChanged(nameof(ServerName));
	}
}
