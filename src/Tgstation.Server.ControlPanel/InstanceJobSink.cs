using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Client;
using Tgstation.Server.Client.Components;
using Tgstation.Server.ControlPanel.ViewModels;

namespace Tgstation.Server.ControlPanel
{
	sealed class InstanceJobSink : IInstanceJobSink, IDisposable
	{

		public Instance Instance { get; }
		public Task Updated => updated.Task;

		readonly JobManagerViewModel jobManagerViewModel;
		readonly Dictionary<long, Job> trackedJobs;
		readonly Dictionary<long, Func<CancellationToken, Task>> postActions;
		readonly CancellationTokenSource cancellationTokenSource;
		readonly List<Job> newestJobs;
		readonly Func<User> currentUserProvider;

		TaskCompletionSource<object> updated;

		public InstanceJobSink(Instance instance, JobManagerViewModel jobManagerViewModel, Func<User> currentUserProvider)
		{
			Instance = instance;
			this.jobManagerViewModel = jobManagerViewModel;
			this.currentUserProvider = currentUserProvider ?? throw new ArgumentNullException(nameof(currentUserProvider));

			trackedJobs = new Dictionary<long, Job>();
			cancellationTokenSource = new CancellationTokenSource();
			updated = new TaskCompletionSource<object>();
			newestJobs = new List<Job>();
			postActions = new Dictionary<long, Func<CancellationToken, Task>>();
		}

		public IObservable<Job> NewJobs()
		{
			IObservable<Job> result;
			lock (newestJobs)
			{
				result = newestJobs.ToList().ToObservable();
				newestJobs.Clear();
			}
			return result;
		}

		public void Dispose()
		{
			cancellationTokenSource.Cancel();
			cancellationTokenSource.Dispose();
		}

		public void RegisterJob(Job job, Func<CancellationToken, Task> onStopped = null)
		{
			if (job == null)
				throw new ArgumentNullException(nameof(job));
			job.StartedBy = currentUserProvider() ?? job.StartedBy;
			lock (trackedJobs)
			{
				if (!trackedJobs.ContainsKey(job.Id))
				{
					trackedJobs.Add(job.Id, job);
					postActions.Add(job.Id, onStopped);
					lock (newestJobs)
						newestJobs.Add(job);
				}
				updated.SetResult(null);
				updated = new TaskCompletionSource<object>();
			}
		}

		public bool DeregisterJob(Job job, out Func<CancellationToken, Task> postAction)
		{
			lock (trackedJobs)
			{
				var result = trackedJobs.Remove(job.Id);
				if (result)
				{
					postAction = postActions[job.Id];
					postActions.Remove(job.Id);
				}
				else
					postAction = null;
				return result;
			}
		}

		public bool TracksJob(Job job)
		{
			lock (trackedJobs)
				return trackedJobs.TryGetValue(job.Id, out job);
		}

		public async Task InitialQuery(IJobsClient jobsClient, CancellationToken cancellationToken)
		{
			var jobs = await jobsClient.ListActive(cancellationToken).ConfigureAwait(false);
			lock (trackedJobs)
				foreach (var I in jobs)
					RegisterJob(I);
		}

		public IObservable<Job> UpdateJobs(IJobsClient jobsClient, CancellationToken cancellationToken)
		{
			var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, cancellationToken);
			var tokenToUse = linkedSource.Token;
			async Task<Job> WrapJobDisconnected(Job job)
			{
				try
				{
					return await jobsClient.GetId(job, tokenToUse).ConfigureAwait(false);
				}
				//caught exceptions here indicate
				//lost access to instance, will be refreshed when regained
				catch (InsufficientPermissionsException)
				{
					return null;
				}
				catch (HttpRequestException)
				{
					return null;
				}
				catch (OperationCanceledException)
				{
					return null;
				}
				catch (ObjectDisposedException)
				{
					return null;
				}
			}
			var tasks = new List<Task<Job>>();
			lock (trackedJobs)
				foreach (var I in trackedJobs)
					tasks.Add(WrapJobDisconnected(I.Value));

			Task.WhenAll(tasks).ContinueWith((a) => linkedSource.Dispose());

			return tasks.Select(x => x.ToObservable()).Merge();
		}
	}
}
