using Avalonia.Media;
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
	sealed class DreamDaemonViewModel : ViewModelBase, ITreeNode, ICommandReceiver<DreamDaemonViewModel.DreamDaemonCommand>
	{
		public enum DreamDaemonCommand
		{
			Close,
			Refresh,
			Start,
			Stop,
			Update,
			Restart
		}

		public string Title => "Dream Daemon";

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
				this.RaisePropertyChanged(nameof(ActiveCompileJob));
				this.RaisePropertyChanged(nameof(StatusString));
				this.RaisePropertyChanged(nameof(StatusColour));
				this.RaisePropertyChanged(nameof(Port));
				this.RaisePropertyChanged(nameof(WebClient));
				this.RaisePropertyChanged(nameof(CurrentSecurity));
				this.RaisePropertyChanged(nameof(StagedCompileJob));
				this.RaisePropertyChanged(nameof(Trusted));
				this.RaisePropertyChanged(nameof(Safe));
				this.RaisePropertyChanged(nameof(Graceful));
				this.RaisePropertyChanged(nameof(HasRevision));
				this.RaisePropertyChanged(nameof(HasStagedRevision));
				onRunningChanged(model?.Running != false);
			}
		}

		public string RestartWord => confirmingRestart ? "Confirm?" : "Restart Server";
		public string StopWord => confirmingShutdown ? "Confirm?" : "Stop Server";

		public string WebClient => (Model?.CurrentAllowWebclient ?? Model?.AllowWebClient)?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";
		public string Port => (Model?.CurrentPort ?? Model?.PrimaryPort)?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";
		public string Graceful => Model == null || !CanRevision ? "Unknown" : Model.SoftRestart.Value ? "Restart" : Model.SoftShutdown.Value ? "Stop" : "None";

		public string CurrentSecurity => !(Model?.CurrentSecurity ?? Model?.SecurityLevel).HasValue ? "Unknown" : Model.CurrentSecurity == DreamDaemonSecurity.Safe ? "Safe" : Model.CurrentSecurity == DreamDaemonSecurity.Ultrasafe ? "Ultrasafe" : "Trusted";
		public string StatusString => !(Model?.Running).HasValue ? "Unknown" : Model.Running.Value ? "Active" : "Inactive";

		public IBrush StatusColour => new SolidColorBrush(!(Model?.Running).HasValue ? Colors.Black : Model.Running.Value ? Colors.Green : Colors.Red);

		public IReadOnlyList<CompileJobViewModel> ActiveCompileJob => Model?.ActiveCompileJob != null ? new List<CompileJobViewModel> { new CompileJobViewModel(Model.ActiveCompileJob) } : null;
		public IReadOnlyList<CompileJobViewModel> StagedCompileJob => Model?.StagedCompileJob != null ? new List<CompileJobViewModel> { new CompileJobViewModel(Model.StagedCompileJob) } : null;

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
				if (!value)
					return;
				Model.SecurityLevel = DreamDaemonSecurity.Safe;
				this.RaisePropertyChanged(nameof(Trusted));
				this.RaisePropertyChanged(nameof(Safe));
			}
		}

		public bool Trusted
		{
			get => Model?.SecurityLevel == DreamDaemonSecurity.Trusted;
			set
			{
				if (!value)
					return;
				Model.SecurityLevel = DreamDaemonSecurity.Trusted;
				this.RaisePropertyChanged(nameof(Safe));
				this.RaisePropertyChanged(nameof(Trusted));
			}
		}

		public uint NewStartupTimeout
		{
			get => newStartupTimeout;
			set => this.RaiseAndSetIfChanged(ref newStartupTimeout, value);
		}
		public ushort NewPrimaryPort
		{
			get => newPrimaryPort;
			set
			{
				this.RaiseAndSetIfChanged(ref newPrimaryPort, value);
				Update.Recheck();
			}
		}

		public ushort NewSecondaryPort
		{
			get => newSecondaryPort;
			set
			{
				this.RaiseAndSetIfChanged(ref newSecondaryPort, value);
				Update.Recheck();
			}
		}

		public bool NewAllowWebClient
		{
			get => newAllowWebClient;
			set => this.RaiseAndSetIfChanged(ref newAllowWebClient, value);
		}

		public bool NewAutoStart
		{
			get => newAutoStart;
			set => this.RaiseAndSetIfChanged(ref newAutoStart, value);
		}

		public bool HasRevision => CanRevision && Model.ActiveCompileJob != null;
		public bool HasStagedRevision => HasRevision && Model.StagedCompileJob != null;


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
		public EnumCommand<DreamDaemonCommand> Restart { get; }

		readonly PageContextViewModel pageContext;
		readonly IDreamDaemonClient dreamDaemonClient;
		readonly IInstanceJobSink jobSink;
		readonly IInstanceUserRightsProvider rightsProvider;
		readonly Action<bool> onRunningChanged;

		DreamDaemon model;

		DreamDaemonSecurity? initalSecurityLevel;
		
		uint newStartupTimeout;
		ushort newPrimaryPort;
		ushort newSecondaryPort;
		bool newAutoStart;
		bool newAllowWebClient;

		bool refreshing;

		bool confirmingRestart;
		bool confirmingShutdown;

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
			Restart = new EnumCommand<DreamDaemonCommand>(DreamDaemonCommand.Restart, this);

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

		void LoadModel(DreamDaemon model)
		{
			using (DelayChangeNotifications())
			{
				Model = model;
				NewStartupTimeout = Model.StartupTimeout ?? 0;
				NewPrimaryPort = Model.PrimaryPort ?? 0;
				NewSecondaryPort = Model.SecondaryPort ?? 0;
				NewAutoStart = Model.AutoStart ?? false;
				NewAllowWebClient = Model.AllowWebClient ?? false;
				initalSecurityLevel = Model.SecurityLevel;

				ClearSoft = true;
				if (CanMetadata)
					if (Model.SoftRestart.Value)
						SoftRestart = true;
					else if (Model.SoftShutdown.Value)
						SoftStop = true;
			}
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

				LoadModel(await dreamDaemonClient.Read(cancellationToken).ConfigureAwait(true));
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
					return !Refreshing && rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.Start) && Model?.Running != true;
				case DreamDaemonCommand.Stop:
					return !Refreshing && rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.Shutdown) && Model?.Running != false;
				case DreamDaemonCommand.Restart:
					return !Refreshing && rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.Restart) && Model?.Running != false;
				case DreamDaemonCommand.Update:
					return !Refreshing && (CanAutoStart || CanPort || CanWebClient || CanSecurity || CanSoftRestart || CanSoftStop || CanTimeout) && !(NewPrimaryPort != 0 && NewPrimaryPort == NewSecondaryPort);
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
						jobSink.RegisterJob(job, DoRefresh);
					}
					finally
					{
						Refreshing = false;
					}
					break;
				case DreamDaemonCommand.Stop:
					if (!confirmingShutdown)
					{
						async void ResetShutdown()
						{
							await Task.Delay(TimeSpan.FromSeconds(3), default);
							confirmingShutdown = false;
							this.RaisePropertyChanged(nameof(StopWord));
						}
						confirmingShutdown = true;
						this.RaisePropertyChanged(nameof(StopWord));
						ResetShutdown();
					}
					else
					{
						await dreamDaemonClient.Shutdown(cancellationToken).ConfigureAwait(true);
						await DoRefresh(cancellationToken).ConfigureAwait(true);
					}
					break;
				case DreamDaemonCommand.Update:
					Refreshing = true;
					try
					{
						var newModel = new DreamDaemon
						{
							AllowWebClient = CanWebClient && Model.AllowWebClient != NewAllowWebClient ? (bool?)NewAllowWebClient : null,
							AutoStart = CanAutoStart && Model.AutoStart != NewAutoStart ? (bool?)NewAutoStart : null,
							PrimaryPort = CanPort && NewPrimaryPort != Model.PrimaryPort ? (ushort?)NewPrimaryPort : null,
							SecondaryPort = CanPort && NewSecondaryPort != Model.SecondaryPort ? (ushort?)NewSecondaryPort : null,
							SecurityLevel = CanSecurity && Model.SecurityLevel != initalSecurityLevel ? Model.SecurityLevel : null,
							StartupTimeout = CanTimeout && Model.StartupTimeout != NewStartupTimeout ? (uint?)NewStartupTimeout : null
						};

						if (CanSoftRestart)
							newModel.SoftRestart = SoftRestart;
						if (CanSoftStop)
							newModel.SoftShutdown = SoftStop;

						LoadModel(await dreamDaemonClient.Update(newModel, cancellationToken).ConfigureAwait(true));
					}
					finally
					{
						Refreshing = false;
					}
					break;
				case DreamDaemonCommand.Restart:
					if (!confirmingRestart)
					{
						async void ResetRestart()
						{
							await Task.Delay(TimeSpan.FromSeconds(3), default);
							confirmingRestart = false;
							this.RaisePropertyChanged(nameof(RestartWord));
						}
						confirmingRestart = true;
						this.RaisePropertyChanged(nameof(RestartWord));
						ResetRestart();
					}
					else
					{
						await dreamDaemonClient.Restart(cancellationToken).ConfigureAwait(true);
						await DoRefresh(cancellationToken).ConfigureAwait(true);
					}
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}
