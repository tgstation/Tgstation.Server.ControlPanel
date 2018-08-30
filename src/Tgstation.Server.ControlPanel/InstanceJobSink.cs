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
		readonly JobManagerViewModel jobManagerViewModel;
		public Instance Instance { get; }
		readonly Dictionary<long, Job> trackedJobs;
		readonly CancellationTokenSource cancellationTokenSource;

		public InstanceJobSink(Instance instance, JobManagerViewModel jobManagerViewModel)
		{
			Instance = instance;
			this.jobManagerViewModel = jobManagerViewModel;

			trackedJobs = new Dictionary<long, Job>();
			cancellationTokenSource = new CancellationTokenSource();
		}

		public void Dispose()
		{
			cancellationTokenSource.Cancel();
			cancellationTokenSource.Dispose();
		}

		public void RegisterJob(Job job)
		{
			lock (trackedJobs)
				if (!trackedJobs.ContainsKey(job.Id))
					trackedJobs.Add(job.Id, job);
		}

		public bool DeregisterJob(Job job)
		{
			lock (trackedJobs)
				return trackedJobs.Remove(job.Id);
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
