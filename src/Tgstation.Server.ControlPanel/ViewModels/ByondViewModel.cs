using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
			Refresh,
			Close
		}
		public string Title => "Byond";

		public string Icon
		{
			get => icon;
			set => this.RaiseAndSetIfChanged(ref icon, value);
		}

		public string CurrentVersion => data != null ? FormatByondVersion(data) : "Unknown";
		public IReadOnlyList<string> InstalledVersions
		{
			get => installedVersions;
			set
			{
				this.RaiseAndSetIfChanged(ref installedVersions, value);
				this.RaisePropertyChanged(nameof(HasInstalledVersions));
			}
		}
		public bool HasInstalledVersions => InstalledVersions != null;

		public bool IsExpanded { get; set; }

		public bool Refreshing
		{
			get => refreshing;
			set => this.RaiseAndSetIfChanged(ref refreshing, value);
		}

		public bool CanRead => rightsProvider.ByondRights.HasFlag(ByondRights.ReadActive);
		public bool CanList => rightsProvider.ByondRights.HasFlag(ByondRights.ListInstalled);
		public bool CanInstall => rightsProvider.ByondRights.HasFlag(ByondRights.ChangeVersion);

		public int NewMajor { get; set; }
		public int NewMinor { get; set; }

		public IReadOnlyList<ITreeNode> Children => null;

		public EnumCommand<ByondCommand> Refresh { get; }
		public EnumCommand<ByondCommand> Update { get; }
		public EnumCommand<ByondCommand> Close { get; }

		readonly PageContextViewModel pageContext;
		readonly IByondClient byondClient;
		readonly IInstanceJobSink jobSink;
		readonly IInstanceUserRightsProvider rightsProvider;

		IReadOnlyList<string> installedVersions;
		Byond data;
		string icon;
		bool refreshing;

		public ByondViewModel(PageContextViewModel pageContext, IByondClient byondClient, IInstanceJobSink jobSink, IInstanceUserRightsProvider rightsProvider)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.byondClient = byondClient ?? throw new ArgumentNullException(nameof(byondClient));
			this.jobSink = jobSink ?? throw new ArgumentNullException(nameof(jobSink));
			this.rightsProvider = rightsProvider ?? throw new ArgumentNullException(nameof(rightsProvider));

			Refresh = new EnumCommand<ByondCommand>(ByondCommand.Refresh, this);
			Update = new EnumCommand<ByondCommand>(ByondCommand.Update, this);

			rightsProvider.OnUpdated += (a, b) =>
			{
				this.RaisePropertyChanged(nameof(CanRead));
				this.RaisePropertyChanged(nameof(CanInstall));
				this.RaisePropertyChanged(nameof(CanList));

				Refresh.Recheck();
				Update.Recheck();
			};

			NewMajor = 511;
			NewMinor = 1385;

			async void InitialLoad() => await Load(default).ConfigureAwait(false);
			InitialLoad();
		}

		static string FormatByondVersion(Byond byond) => byond.Version == null ? "None" : String.Format(CultureInfo.InvariantCulture, "{0}.{1}", byond.Version.Major, byond.Version.Minor);

		async Task Load(CancellationToken cancellationToken)
		{
			if (!CanRead && !CanList)
			{
				Icon = CanInstall ? "resm:Tgstation.Server.ControlPanel.Assets.byond.jpg" : "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg";
				return;
			}

			Icon = "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png";
			Refreshing = true;
			Refresh.Recheck();
			Update.Recheck();
			try
			{
				var dataTask = byondClient.ActiveVersion(cancellationToken);
				var installTask = byondClient.InstalledVersions(cancellationToken);
				data = await dataTask.ConfigureAwait(true);
				this.RaisePropertyChanged(CurrentVersion);
				InstalledVersions = (await installTask.ConfigureAwait(true)).Select(x => FormatByondVersion(x)).ToList();
			}
			finally
			{
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.byond.jpg";
				Refreshing = false;
				Refresh.Recheck();
				Update.Recheck();
			}
		}
		public Task HandleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(ByondCommand command)
		{
			switch (command)
			{
				case ByondCommand.Refresh:
					return !Refreshing && (CanRead || CanList);
				case ByondCommand.Update:
					return !Refreshing && CanInstall;
				case ByondCommand.Close:
					return true;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public async Task RunCommand(ByondCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case ByondCommand.Refresh:
					await Load(cancellationToken).ConfigureAwait(true);
					break;
				case ByondCommand.Update:
					Refreshing = true;
					Refresh.Recheck();
					Update.Recheck();
					try
					{
						data = await byondClient.SetActiveVersion(new Byond
						{
							Version = new Version(NewMajor, NewMinor)
						}, cancellationToken).ConfigureAwait(true);
						if (data.InstallJob != null)
							jobSink.RegisterJob(data.InstallJob, Load);
						this.RaisePropertyChanged(CurrentVersion);
					}
					finally
					{
						Refreshing = false;
						Refresh.Recheck();
						Update.Recheck();
					}
					break;
				case ByondCommand.Close:
					pageContext.ActiveObject = null;
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}
