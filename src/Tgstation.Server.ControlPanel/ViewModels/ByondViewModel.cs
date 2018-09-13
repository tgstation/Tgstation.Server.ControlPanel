using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class ByondViewModel : ViewModelBase, ITreeNode, ICommandReceiver<ByondViewModel.ByondCommand>
	{
		public enum ByondCommand
		{
			Update,
			Refresh
		}
		public string Title => "Byond";

		public string Icon
		{
			get => icon;
			set => this.RaiseAndSetIfChanged(ref icon, value);
		}

		public string CurrentVersion => data != null ? String.Format(CultureInfo.InvariantCulture, "{0}.{1}", data.Version.Major, data.Version.Minor) : "Unknown";

		public bool IsExpanded { get; set; }

		public bool CanRead => rightsProvider.ByondRights.HasFlag(ByondRights.ReadActive);
		public bool CanList => rightsProvider.ByondRights.HasFlag(ByondRights.ListInstalled);
		public bool CanInstall => rightsProvider.ByondRights.HasFlag(ByondRights.ChangeVersion);

		public IReadOnlyList<ITreeNode> Children => null;

		readonly PageContextViewModel pageContext;
		readonly IByondClient byondClient;
		readonly IInstanceJobSink jobSink;
		readonly IInstanceUserRightsProvider rightsProvider;

		Byond data;
		string icon;

		public ByondViewModel(PageContextViewModel pageContext, IByondClient byondClient, IInstanceJobSink jobSink, IInstanceUserRightsProvider rightsProvider)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.byondClient = byondClient ?? throw new ArgumentNullException(nameof(byondClient));
			this.jobSink = jobSink ?? throw new ArgumentNullException(nameof(jobSink));
			this.rightsProvider = rightsProvider ?? throw new ArgumentNullException(nameof(rightsProvider));

			rightsProvider.OnUpdated += (a, b) =>
			{
				this.RaisePropertyChanged(nameof(CanRead));
				this.RaisePropertyChanged(nameof(CanInstall));
				this.RaisePropertyChanged(nameof(CanList));
			};

			async void InitialLoad() => await Load(default).ConfigureAwait(false);
			InitialLoad();
		}

		async Task Load(CancellationToken cancellationToken)
		{
			if (!CanRead && !CanList)
			{
				Icon = CanInstall ? "resm:Tgstation.Server.ControlPanel.Assets.byond.jpg" : "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg";
				return;
			}

			Icon = "resm:Tgstation.Server.ControlPanel.Assets.loading.png";
			try
			{
				var dataTask = byondClient.Read(cancellationToken);
				//var installTask = 
				data = await dataTask.ConfigureAwait(true);
				if (data.InstallJob != null)
					jobSink.RegisterJob(data.InstallJob);
				this.RaisePropertyChanged(CurrentVersion);
			}
			finally
			{
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.byond.jpg";
			}
		}

		public bool CanRunCommand(ByondCommand command)
		{
			throw new NotImplementedException();
		}

		public Task HandleClick(CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task RunCommand(ByondCommand command, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}
	}
}
