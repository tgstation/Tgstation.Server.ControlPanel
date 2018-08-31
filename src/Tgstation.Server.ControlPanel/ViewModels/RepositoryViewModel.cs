using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class RepositoryViewModel : ViewModelBase, ITreeNode
	{
		public string Title => "Repository";

		public string Icon
		{
			get => icon;
			private set => this.RaiseAndSetIfChanged(ref icon, value);
		}

		public IReadOnlyList<ITreeNode> Children => null;

		public bool IsExpanded { get; set; }

		public string Origin => repository?.Origin;


		readonly PageContextViewModel pageContext;
		readonly IRepositoryClient repositoryClient;

		Repository repository;

		string icon;

		public RepositoryViewModel(PageContextViewModel pageContext, IRepositoryClient repositoryClient)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.repositoryClient = repositoryClient ?? throw new ArgumentNullException(nameof(repositoryClient));

			async void InitialLoad() => await Refresh(default).ConfigureAwait(false);
			InitialLoad();
		}

		async Task Refresh(CancellationToken cancellationToken)
		{
			Icon = "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png";
			try
			{
				repository = await repositoryClient.Read(cancellationToken).ConfigureAwait(true);
			}
			finally
			{
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.git.png";
			}
		}

		public Task HandleDoubleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}
	}
}
