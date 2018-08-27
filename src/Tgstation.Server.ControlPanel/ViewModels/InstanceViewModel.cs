using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Client;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class InstanceViewModel : ViewModelBase, ITreeNode
	{
		public string Title => instance.Name;

		public bool IsExpanded { get; set; }

		public string Icon => instance.Online.Value ? (ddRunning ? "resm:Tgstation.Server.ControlPanel.Assets.database_on.jpg" : "resm:Tgstation.Server.ControlPanel.Assets.database.png") : "resm:Tgstation.Server.ControlPanel.Assets.database_down.png";

		public IReadOnlyList<ITreeNode> Children => null;	//TODO

		readonly IInstanceManagerClient instanceManagerClient;
		readonly IInstanceClient instanceClient;
		readonly PageContextViewModel pageContext;

		Instance instance;
		bool ddRunning;

		public InstanceViewModel(IInstanceManagerClient instanceManagerClient, PageContextViewModel pageContext, Instance instance)
		{
			this.instanceManagerClient = instanceManagerClient ?? throw new ArgumentNullException(nameof(instanceManagerClient));
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.instance = instance ?? throw new ArgumentNullException(nameof(instance));

			instanceClient = instanceManagerClient.CreateClient(instance);
		}

		public void SetDDRunning(bool yes)
		{
			ddRunning = yes;
			this.RaisePropertyChanged(nameof(Icon));
		}

		async Task Refresh(CancellationToken cancellationToken)
		{
			//instance = await instanceManagerClient.GetId(instance, cancellationToken).ConfigureAwait(true);
			await Task.Delay(1, cancellationToken).ConfigureAwait(true);
		}

		public Task HandleDoubleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Refresh(cancellationToken);
		}
	}
}