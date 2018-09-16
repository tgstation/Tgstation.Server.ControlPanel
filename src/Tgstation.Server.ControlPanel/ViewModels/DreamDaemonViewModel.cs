using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class DreamDaemonViewModel : ViewModelBase, ITreeNode, ICommandReceiver<DreamDaemonViewModel.DreamDaemonCommand>
	{
		public enum DreamDaemonCommand
		{
			Close,
			Refresh,
			Start,
			Stop,
			Update
		}

		public string Title => "DreamDaemon";

		public string Icon => Refreshing ? "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png" : rightsProvider.DreamDaemonRights == DreamDaemonRights.None ? "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg" : Model?.Running != false ? "resm:Tgstation.Server.ControlPanel.Assets.dd.ico" : "resm:Tgstation.Server.ControlPanel.Assets.dd_down.ico";

		public bool IsExpanded { get; set; }

		public IReadOnlyList<ITreeNode> Children => null;

		public DreamDaemon Model
		{
			get => model;
			set
			{
				this.RaiseAndSetIfChanged(ref model, value);
				this.RaisePropertyChanged(nameof(Icon));
				onRunningChanged(model?.Running != false);
			}
		}

		public bool Refreshing
		{
			get => refreshing;
			set
			{
				Start.Recheck();
				Stop.Recheck();
				Update.Recheck();
				Refresh.Recheck();
				this.RaiseAndSetIfChanged(ref refreshing, value);
				this.RaisePropertyChanged(nameof(Icon));
			}
		}

		public bool SoftRestart
		{
			get => softRestart;
			set
			{
				this.RaiseAndSetIfChanged(ref softRestart, value);
				if (value)
				{
					SoftStop = false;
					ClearSoft = false;
				}
			}
		}

		public bool SoftStop
		{
			get => softStop;
			set
			{
				this.RaiseAndSetIfChanged(ref softStop, value);
				if (value)
				{
					ClearSoft = false;
					SoftRestart = false;
				}
			}
		}

		public bool ClearSoft
		{
			get => clearSoft;
			set
			{
				this.RaiseAndSetIfChanged(ref clearSoft, value);
				if (value)
				{
					SoftStop = false;
					SoftRestart = false;
				}
			}
		}

		public bool Safe
		{
			get => Model?.SecurityLevel == DreamDaemonSecurity.Safe;
			set
			{
				Model.SecurityLevel = DreamDaemonSecurity.Safe;
				this.RaisePropertyChanged(nameof(Trusted));
			}
		}

		public bool Trusted
		{
			get => Model?.SecurityLevel == DreamDaemonSecurity.Trusted;
			set
			{
				Model.SecurityLevel = DreamDaemonSecurity.Trusted;
				this.RaisePropertyChanged(nameof(Safe));
			}
		}
		
		public bool CanPort => rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.SetPorts);
		public bool CanAutoStart => rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.SetAutoStart);
		public bool CanSecurity => rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.SetSecurity);
		public bool CanWebClient => rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.SetWebClient);
		public bool CanTimeout => rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.SetStartupTimeout);
		public bool CanSoftRestart => rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.SoftRestart);
		public bool CanSoftStop => rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.SoftShutdown);
		public bool CanMetadata => rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.ReadMetadata);
		public bool CanRevision => rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.ReadRevision);

		public EnumCommand<DreamDaemonCommand> Close { get; }
		public EnumCommand<DreamDaemonCommand> Refresh { get; }
		public EnumCommand<DreamDaemonCommand> Start { get; }
		public EnumCommand<DreamDaemonCommand> Stop { get; }
		public EnumCommand<DreamDaemonCommand> Update { get; }

		readonly PageContextViewModel pageContext;
		readonly IDreamDaemonClient dreamDaemonClient;
		readonly IInstanceJobSink jobSink;
		readonly IInstanceUserRightsProvider rightsProvider;
		readonly Action<bool> onRunningChanged;

		DreamDaemon model;

		bool refreshing;

		bool softRestart;
		bool softStop;
		bool clearSoft;

		public DreamDaemonViewModel(PageContextViewModel pageContext, IDreamDaemonClient dreamDaemonClient, IInstanceJobSink jobSink, IInstanceUserRightsProvider rightsProvider, Action<bool> onRunningChanged)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.dreamDaemonClient = dreamDaemonClient ?? throw new ArgumentNullException(nameof(dreamDaemonClient));
			this.jobSink = jobSink ?? throw new ArgumentNullException(nameof(jobSink));
			this.rightsProvider = rightsProvider ?? throw new ArgumentNullException(nameof(rightsProvider));
			this.onRunningChanged = onRunningChanged ?? throw new ArgumentNullException(nameof(onRunningChanged));

			Close = new EnumCommand<DreamDaemonCommand>(DreamDaemonCommand.Close, this);
			Refresh = new EnumCommand<DreamDaemonCommand>(DreamDaemonCommand.Refresh, this);
			Start = new EnumCommand<DreamDaemonCommand>(DreamDaemonCommand.Start, this);
			Stop = new EnumCommand<DreamDaemonCommand>(DreamDaemonCommand.Stop, this);
			Update = new EnumCommand<DreamDaemonCommand>(DreamDaemonCommand.Update, this);

			rightsProvider.OnUpdated += (a, b) =>
			{
				Start.Recheck();
				Stop.Recheck();
				Update.Recheck();

				using (DelayChangeNotifications())
				{
					this.RaisePropertyChanged(nameof(Icon));

					this.RaisePropertyChanged(nameof(CanPort));
					this.RaisePropertyChanged(nameof(CanAutoStart));
					this.RaisePropertyChanged(nameof(CanSecurity));
					this.RaisePropertyChanged(nameof(CanWebClient));
					this.RaisePropertyChanged(nameof(CanTimeout));
					this.RaisePropertyChanged(nameof(CanSoftRestart));
					this.RaisePropertyChanged(nameof(CanSoftStop));
					this.RaisePropertyChanged(nameof(CanMetadata));
					this.RaisePropertyChanged(nameof(CanRevision));
				}
			};

			async void InitialLoad() => await DoRefresh(default).ConfigureAwait(true);
			InitialLoad();
		}

		async Task DoRefresh(CancellationToken cancellationToken)
		{
			Refreshing = true;
			try
			{
				if (!CanRevision && !CanPort)
					using (DelayChangeNotifications())
					{
						Model = new DreamDaemon();
						ClearSoft = true;
						return;
					}

				Model = await dreamDaemonClient.Read(cancellationToken).ConfigureAwait(true);
				using (DelayChangeNotifications())
				{
					this.RaisePropertyChanged(nameof(Trusted));
					this.RaisePropertyChanged(nameof(Safe));

					ClearSoft = true;
					if (CanMetadata)
						if (Model.SoftRestart.Value)
							SoftRestart = true;
						else if (Model.SoftShutdown.Value)
							SoftStop = true;
				}
			}
			finally
			{
				Refreshing = false;
			}
		}

		public Task HandleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(DreamDaemonCommand command)
		{
			switch (command)
			{
				case DreamDaemonCommand.Close:
					return true;
				case DreamDaemonCommand.Refresh:
					return !Refreshing;
				case DreamDaemonCommand.Start:
					return !Refreshing && rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.Start);
				case DreamDaemonCommand.Stop:
					return !Refreshing && rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.Shutdown);
				case DreamDaemonCommand.Update:
					return !Refreshing && (CanAutoStart || CanPort || CanWebClient || CanSecurity || CanSoftRestart || CanSoftStop || CanTimeout);
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public async Task RunCommand(DreamDaemonCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case DreamDaemonCommand.Close:
					pageContext.ActiveObject = null;
					break;
				case DreamDaemonCommand.Refresh:
					await DoRefresh(cancellationToken).ConfigureAwait(true);
					break;
				case DreamDaemonCommand.Start:
					Refreshing = true;
					try
					{
						var job = await dreamDaemonClient.Start(cancellationToken).ConfigureAwait(true);
						jobSink.RegisterJob(job);
					}
					finally
					{
						Refreshing = false;
					}
					break;
				case DreamDaemonCommand.Stop:
					await dreamDaemonClient.Shutdown(cancellationToken).ConfigureAwait(true);
					await DoRefresh(cancellationToken).ConfigureAwait(true);
					break;
				case DreamDaemonCommand.Update:
					Refreshing = true;
					try
					{
						var newModel = new DreamDaemon
						{
							AllowWebClient = CanWebClient ? Model.AllowWebClient : null,
							AutoStart = CanAutoStart ? Model.AutoStart : null,
							PrimaryPort = CanPort ? Model.PrimaryPort : null,
							SecondaryPort = CanPort ? Model.SecondaryPort : null,
							SecurityLevel = CanSecurity ? Model.SecurityLevel : null,
							StartupTimeout = CanTimeout ? Model.StartupTimeout : null
						};

						if (CanSoftRestart)
							newModel.SoftRestart = SoftRestart;
						if (CanSoftStop)
							newModel.SoftShutdown = SoftStop;

						Model = await dreamDaemonClient.Update(newModel, cancellationToken).ConfigureAwait(true);
					}
					finally
					{
						Refreshing = false;
					}
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}
