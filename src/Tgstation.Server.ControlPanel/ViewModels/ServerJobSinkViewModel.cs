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
		readonly Func<IServerClient> clientProvider;
		readonly JobManagerViewModel jobManagerViewModel;

		readonly Dictionary<long, InstanceJobSink> instanceSinks;

		readonly Action onDisposed;

		public ServerJobSinkViewModel(Func<IServerClient> clientProvider, JobManagerViewModel jobManagerViewModel, Action onDisposed)
		{
			this.clientProvider = clientProvider;
			this.jobManagerViewModel = jobManagerViewModel;
			this.onDisposed = onDisposed;

			instanceSinks = new Dictionary<long, InstanceJobSink>();
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
	}
}
