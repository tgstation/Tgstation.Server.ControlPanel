using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using ReactiveUI;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Models.Request;
using Tgstation.Server.Api.Models.Response;
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
			Restart,
			Dump,
			Join
		}

		public string Title => "Dream Daemon";

		public string Icon => Refreshing ? "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png" : rightsProvider.DreamDaemonRights == DreamDaemonRights.None ? "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg" : Model?.Status != WatchdogStatus.Offline ? "resm:Tgstation.Server.ControlPanel.Assets.dd.ico" : "resm:Tgstation.Server.ControlPanel.Assets.dd_down.ico";

		public bool IsExpanded { get; set; }

		public IReadOnlyList<ITreeNode> Children => null;

		public DreamDaemonResponse Model
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
				this.RaisePropertyChanged(nameof(NewAdditionalParams));
				if (model != null)
				{
					SoftRestart = model.SoftRestart.Value;
					SoftStop = model.SoftShutdown.Value;
				}

				onRunningChanged(model?.Status != WatchdogStatus.Offline);
			}
		}

		public string RestartWord => confirmingRestart ? "Confirm?" : "Restart Server";
		public string StopWord => confirmingShutdown ? "Confirm?" : "Stop Server";

		public string WebClient => (Model?.CurrentAllowWebclient ?? Model?.AllowWebClient)?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";
		public string Port => (Model?.CurrentPort ?? Model?.Port)?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";
		public string Graceful => Model == null || !CanRevision ? "Unknown" : Model.SoftRestart.Value ? "Restart" : Model.SoftShutdown.Value ? "Stop" : "None";

		public string CurrentSecurity => !(Model?.CurrentSecurity ?? Model?.SecurityLevel).HasValue ? "Unknown" : Model.CurrentSecurity == DreamDaemonSecurity.Safe ? "Safe" : Model.CurrentSecurity == DreamDaemonSecurity.Ultrasafe ? "Ultrasafe" : "Trusted";
		public string StatusString => !(Model?.Status).HasValue ? "Unknown" : Model.Status.ToString();

		public IBrush StatusColour => new SolidColorBrush(!(Model?.Status).HasValue ? Colors.Black :
			Model.Status.Value == WatchdogStatus.Online ? Colors.Green :
			(Model.Status.Value == WatchdogStatus.Offline ? Colors.Red : Colors.Yellow));

		public IReadOnlyList<CompileJobViewModel> ActiveCompileJob => Model?.ActiveCompileJob != null ? new List<CompileJobViewModel> { new CompileJobViewModel(Model.ActiveCompileJob) } : null;
		public IReadOnlyList<CompileJobViewModel> StagedCompileJob => Model?.StagedCompileJob != null ? new List<CompileJobViewModel> { new CompileJobViewModel(Model.StagedCompileJob) } : null;

		public bool Refreshing
		{
			get => refreshing;
			set
			{
				this.RaiseAndSetIfChanged(ref refreshing, value);
				this.RaisePropertyChanged(nameof(Icon));
				Start.Recheck();
				Stop.Recheck();
				Update.Recheck();
				Refresh.Recheck();
				Restart.Recheck();
				Join.Recheck();
			}
		}

		public bool Running => Model.Status == WatchdogStatus.Online;

		public bool SoftRestart
		{
			get => softRestart;
			set => this.RaiseAndSetIfChanged(ref softRestart, value);
		}

		public bool SoftStop
		{
			get => softStop;
			set => this.RaiseAndSetIfChanged(ref softStop, value);
		}

		public bool ClearSoft
		{
			get => clearSoft;
			set => this.RaiseAndSetIfChanged(ref clearSoft, value);
		}

		public bool Ultrasafe
		{
			get => Model?.SecurityLevel == DreamDaemonSecurity.Ultrasafe;
			set => Model.SecurityLevel = value ? DreamDaemonSecurity.Ultrasafe : null;
		}

		public bool Safe
		{
			get => Model?.SecurityLevel == DreamDaemonSecurity.Safe;
			set => Model.SecurityLevel = value ? DreamDaemonSecurity.Safe : null;
		}

		public bool Trusted
		{
			get => Model?.SecurityLevel == DreamDaemonSecurity.Trusted;
			set => Model.SecurityLevel = value ? DreamDaemonSecurity.Trusted : null;
		}

		public uint NewStartupTimeout
		{
			get => newStartupTimeout;
			set => this.RaiseAndSetIfChanged(ref newStartupTimeout, value);
		}

		public uint NewHeartbeatSeconds
		{
			get => newHeartbeatSeconds;
			set => this.RaiseAndSetIfChanged(ref newHeartbeatSeconds, value);
		}

		public string NewAdditionalParams
		{
			get => newAdditionalParams;
			set => this.RaiseAndSetIfChanged(ref newAdditionalParams, value);
		}
		public uint NewTopicTimeout
		{
			get => newTopicTimeout;
			set => this.RaiseAndSetIfChanged(ref newTopicTimeout, value);
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

		public bool HasRevision => CanRevision && Model?.ActiveCompileJob != null;
		public bool HasStagedRevision => HasRevision && Model.StagedCompileJob != null;


		public bool CanPort => rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.SetPort);
		public bool CanAutoStart => rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.SetAutoStart);
		public bool CanSecurity => rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.SetSecurity);
		public bool CanWebClient => rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.SetWebClient);
		public bool CanTimeout => rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.SetStartupTimeout);
		public bool CanTopic => rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.SetTopicTimeout);
		public bool CanSoftRestart => rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.SoftRestart);
		public bool CanSoftStop => rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.SoftShutdown);
		public bool CanMetadata => rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.ReadMetadata);
		public bool CanRevision => rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.ReadRevision);
		public bool CanHeartbeat => rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.SetHeartbeatInterval);
		public bool CanDump => rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.CreateDump);
		public bool CanAdditionalParams => rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.SetAdditionalParameters);

		public EnumCommand<DreamDaemonCommand> Close { get; }
		public EnumCommand<DreamDaemonCommand> Refresh { get; }
		public EnumCommand<DreamDaemonCommand> Start { get; }
		public EnumCommand<DreamDaemonCommand> Stop { get; }
		public EnumCommand<DreamDaemonCommand> Update { get; }
		public EnumCommand<DreamDaemonCommand> Restart { get; }
		public EnumCommand<DreamDaemonCommand> Dump { get; }
		public EnumCommand<DreamDaemonCommand> Join { get; }

		readonly PageContextViewModel pageContext;
		readonly IDreamDaemonClient dreamDaemonClient;
		readonly IInstanceJobSink jobSink;
		readonly IInstanceUserRightsProvider rightsProvider;
		readonly Action<bool> onRunningChanged;

		readonly string serverAddress;

		DreamDaemonResponse model;

		DreamDaemonSecurity? initalSecurityLevel;

		uint newStartupTimeout;
		uint newHeartbeatSeconds;
		string newAdditionalParams;
		uint newTopicTimeout;
		ushort newPrimaryPort;
		bool newAutoStart;
		bool newAllowWebClient;

		bool refreshing;

		bool confirmingRestart;
		bool confirmingShutdown;

		bool softRestart;
		bool softStop;
		bool clearSoft;

		public DreamDaemonViewModel(PageContextViewModel pageContext, IDreamDaemonClient dreamDaemonClient, IInstanceJobSink jobSink, IInstanceUserRightsProvider rightsProvider, Action<bool> onRunningChanged, string serverAddress)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.dreamDaemonClient = dreamDaemonClient ?? throw new ArgumentNullException(nameof(dreamDaemonClient));
			this.jobSink = jobSink ?? throw new ArgumentNullException(nameof(jobSink));
			this.rightsProvider = rightsProvider ?? throw new ArgumentNullException(nameof(rightsProvider));
			this.onRunningChanged = onRunningChanged ?? throw new ArgumentNullException(nameof(onRunningChanged));
			this.serverAddress = serverAddress ?? throw new ArgumentNullException(nameof(serverAddress));

			Close = new EnumCommand<DreamDaemonCommand>(DreamDaemonCommand.Close, this);
			Refresh = new EnumCommand<DreamDaemonCommand>(DreamDaemonCommand.Refresh, this);
			Start = new EnumCommand<DreamDaemonCommand>(DreamDaemonCommand.Start, this);
			Stop = new EnumCommand<DreamDaemonCommand>(DreamDaemonCommand.Stop, this);
			Update = new EnumCommand<DreamDaemonCommand>(DreamDaemonCommand.Update, this);
			Restart = new EnumCommand<DreamDaemonCommand>(DreamDaemonCommand.Restart, this);
			Dump = new EnumCommand<DreamDaemonCommand>(DreamDaemonCommand.Dump, this);
			Join = new EnumCommand<DreamDaemonCommand>(DreamDaemonCommand.Join, this);

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
					this.RaisePropertyChanged(nameof(CanAdditionalParams));
					this.RaisePropertyChanged(nameof(CanDump));
					this.RaisePropertyChanged(nameof(CanHeartbeat));
				}
			};

			async void InitialLoad()
			{
				try
				{
					await DoRefresh(default).ConfigureAwait(true);
				}
				catch (Exception ex)
				{
					MainWindowViewModel.HandleException(ex);
				}
			}
			InitialLoad();
		}

		void LoadModel(DreamDaemonResponse model)
		{
			using (DelayChangeNotifications())
			{
				Model = model;
				NewStartupTimeout = Model.StartupTimeout ?? 0;
				NewTopicTimeout = Model.TopicRequestTimeout ?? 0;
				NewHeartbeatSeconds = Model.HeartbeatSeconds ?? 0;
				NewPrimaryPort = Model.Port ?? 0;
				NewAutoStart = Model.AutoStart ?? false;
				NewAllowWebClient = Model.AllowWebClient ?? false;
				NewAdditionalParams = Model.AdditionalParameters ?? string.Empty;
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
						Model = new DreamDaemonResponse();
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
			return command switch
			{
				DreamDaemonCommand.Close => true,
				DreamDaemonCommand.Refresh => !Refreshing,
				DreamDaemonCommand.Start => !Refreshing && rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.Start) && Model?.Status.Value == WatchdogStatus.Offline,
				DreamDaemonCommand.Stop => !Refreshing && rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.Shutdown) && Model?.Status.Value != WatchdogStatus.Offline,
				DreamDaemonCommand.Restart => !Refreshing && rightsProvider.DreamDaemonRights.HasFlag(DreamDaemonRights.Restart) && Model?.Status.Value != WatchdogStatus.Offline,
				DreamDaemonCommand.Update => !Refreshing && (CanAutoStart || CanPort || CanWebClient || CanSecurity || CanSoftRestart || CanSoftStop || CanTimeout || CanTopic || CanAdditionalParams) && NewPrimaryPort != 0,
				DreamDaemonCommand.Dump => !Refreshing && Model?.Status.Value != WatchdogStatus.Offline && CanDump,
				DreamDaemonCommand.Join => !Refreshing && Model?.Status.Value == WatchdogStatus.Online,
				_ => throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!"),
			};
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
						var newModel = new DreamDaemonRequest
						{
							AllowWebClient = CanWebClient && Model.AllowWebClient != NewAllowWebClient ? (bool?)NewAllowWebClient : null,
							AutoStart = CanAutoStart && Model.AutoStart != NewAutoStart ? (bool?)NewAutoStart : null,
							Port = CanPort && NewPrimaryPort != Model.Port ? (ushort?)NewPrimaryPort : null,
							SecurityLevel = CanSecurity && Model.SecurityLevel != initalSecurityLevel ? Model.SecurityLevel : null,
							StartupTimeout = CanTimeout && Model.StartupTimeout != NewStartupTimeout ? (uint?)NewStartupTimeout : null,
							TopicRequestTimeout = CanTopic && Model.TopicRequestTimeout != NewTopicTimeout ? (uint?)NewTopicTimeout : null,
							HeartbeatSeconds = CanHeartbeat && Model.HeartbeatSeconds != NewHeartbeatSeconds ? (uint?)NewHeartbeatSeconds : null,
							AdditionalParameters = CanAdditionalParams && Model.AdditionalParameters != NewAdditionalParams ? NewAdditionalParams : null,
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
				case DreamDaemonCommand.Dump:
					Refreshing = true;
					try
					{
						var job = await dreamDaemonClient.CreateDump(cancellationToken).ConfigureAwait(true);
						jobSink.RegisterJob(job, DoRefresh);
					}
					finally
					{
						Refreshing = false;
					}
					break;
				case DreamDaemonCommand.Join:
					ControlPanel.LaunchUrl($"byond://{serverAddress}:{Model.CurrentPort}");
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}
