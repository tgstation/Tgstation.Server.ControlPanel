using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Models.Response;
using Tgstation.Server.Client;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel
{
	sealed class InstanceJobSink : IInstanceJobSink, IDisposable
	{

		public Instance Instance { get; }
		public Task Updated => updated.Task;

		readonly Dictionary<long, JobResponse> trackedJobs;
		readonly Dictionary<long, Func<CancellationToken, Task>> postActions;
		readonly CancellationTokenSource cancellationTokenSource;
		readonly List<JobResponse> newestJobs;
		readonly Func<UserResponse> currentUserProvider;

		TaskCompletionSource<object> updated;

		public InstanceJobSink(Instance instance, Func<UserResponse> currentUserProvider)
		{
			Instance = instance;
			this.currentUserProvider = currentUserProvider ?? throw new ArgumentNullException(nameof(currentUserProvider));

			trackedJobs = new Dictionary<long, JobResponse>();
			cancellationTokenSource = new CancellationTokenSource();
			updated = new TaskCompletionSource<object>();
			newestJobs = new List<JobResponse>();
			postActions = new Dictionary<long, Func<CancellationToken, Task>>();
		}

		public IObservable<JobResponse> NewJobs()
		{
			IObservable<JobResponse> result;
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

		public void RegisterJob(JobResponse job, Func<CancellationToken, Task> onStopped = null)
		{
			if (job == null)
				throw new ArgumentNullException(nameof(job));
			job.StartedBy = currentUserProvider() ?? job.StartedBy;
			TaskCompletionSource<object> toComplete;
			lock (trackedJobs)
			{
				if (!trackedJobs.ContainsKey(job.Id.Value))
				{
					trackedJobs.Add(job.Id.Value, job);
					postActions.Add(job.Id.Value, onStopped);
					lock (newestJobs)
						newestJobs.Add(job);
				}
				toComplete = updated;
				updated = new TaskCompletionSource<object>();
			}

			toComplete.SetResult(null);
		}

		public bool DeregisterJob(JobResponse job, out Func<CancellationToken, Task> postAction)
		{
			lock (trackedJobs)
			{
				var result = trackedJobs.Remove(job.Id.Value);
				if (result)
				{
					postAction = postActions[job.Id.Value];
					postActions.Remove(job.Id.Value);
				}
				else
					postAction = null;
				return result;
			}
		}

		public bool TracksJob(JobResponse job)
		{
			lock (trackedJobs)
				return trackedJobs.TryGetValue(job.Id.Value, out job);
		}

		public async Task InitialQuery(IJobsClient jobsClient, CancellationToken cancellationToken)
		{
			var jobs = await jobsClient.ListActive(null, cancellationToken).ConfigureAwait(false);
			lock (trackedJobs)
				foreach (var I in jobs)
					RegisterJob(I);
		}

		public IObservable<JobResponse> UpdateJobs(IJobsClient jobsClient, CancellationToken cancellationToken)
		{
			var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, cancellationToken);
			var tokenToUse = linkedSource.Token;
			async Task<JobResponse> WrapJobDisconnected(JobResponse job)
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
			var tasks = new List<Task<JobResponse>>();
			lock (trackedJobs)
				foreach (var I in trackedJobs)
					tasks.Add(WrapJobDisconnected(I.Value));

			Task.WhenAll(tasks).ContinueWith((a) => linkedSource.Dispose(), cancellationToken);

			return tasks.Select(x => x.ToObservable()).Merge();
		}
	}
}
