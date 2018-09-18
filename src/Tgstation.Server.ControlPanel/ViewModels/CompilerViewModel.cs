using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class CompilerViewModel : ViewModelBase, ITreeNode, ICommandReceiver<CompilerViewModel.CompilerCommand>
	{
		public enum CompilerCommand
		{
			Close,
			Refresh,
			NextPage,
			LastPage,
			Compile,
			Update
		}

		const int JobsPerPage = 10;

		public string Title => "Deployment";

		public string Icon => !(CanRead || CanDme || CanCompile || CanGetJobs || CanPort) ? "resm:Tgstation.Server.ControlPanel.Assets.denied.png" : Refreshing ? "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png" : "resm:Tgstation.Server.ControlPanel.Assets.dreammaker.ico";

		public bool IsExpanded { get; set; }

		public IReadOnlyList<ITreeNode> Children => null;

		public IReadOnlyList<CompileJobViewModel> CurrentPage => jobPages.ContainsKey(selectedPage) ? jobPages[selectedPage] : null;
		public bool Refreshing
		{
			get => refreshing;
			set
			{
				this.RaiseAndSetIfChanged(ref refreshing, value);
				this.RaisePropertyChanged(nameof(Icon));
				this.RaisePropertyChanged(nameof(CanDmeView));
				this.RaisePropertyChanged(nameof(CanSecurityView));
				this.RaisePropertyChanged(nameof(CanPortView));
				RecheckCommands();
			}
		}

		public string ProjectName => Model != null ? Model.ProjectName ?? "<Auto Detect>" : "<Unknown>";

		public bool Safe
		{
			get => newSecurityLevel == DreamDaemonSecurity.Safe;
			set
			{
				this.RaiseAndSetIfChanged(ref newSecurityLevel, DreamDaemonSecurity.Safe);
				this.RaisePropertyChanged(nameof(Trusted));
			}
		}

		public bool Trusted
		{
			get => newSecurityLevel == DreamDaemonSecurity.Trusted;
			set
			{
				this.RaiseAndSetIfChanged(ref newSecurityLevel, DreamDaemonSecurity.Trusted);
				this.RaisePropertyChanged(nameof(Safe));
			}
		}

		public DreamMaker Model
		{
			get => model;
			set
			{
				this.RaiseAndSetIfChanged(ref model, value);
				this.RaisePropertyChanged(nameof(ProjectName));
				this.RaisePropertyChanged(nameof(SecurityLevel));
			}
		}

		public int NewPort
		{
			get => newPort;
			set
			{
				this.RaiseAndSetIfChanged(ref newPort, value);
				Update.Recheck();
			}
		}
		public string NewDme
		{
			get => newDme;
			set
			{
				this.RaiseAndSetIfChanged(ref newDme, value);
				Update.Recheck();
			}
		}

		public bool AutoDetectDme
		{
			get => autoDetectDme;
			set
			{
				this.RaiseAndSetIfChanged(ref autoDetectDme, value);
				if (value)
					NewDme = String.Empty;
				this.RaisePropertyChanged(nameof(CanDmeView));
				Update.Recheck();
			}
		}
		public string SecurityLevel => Model?.ApiValidationSecurityLevel.HasValue ? Model.ApiValidationSecurityLevel.ToString() : "Unknown";

		public int ViewSelectedPage => selectedPage + 1;
		public int ViewNumPages => numPages + 1;

		public bool CanCompile => rightsProvider.DreamMakerRights.HasFlag(DreamMakerRights.Compile);
		public bool CanRead => rightsProvider.DreamMakerRights.HasFlag(DreamMakerRights.Read);
		public bool CanGetJobs => rightsProvider.DreamMakerRights.HasFlag(DreamMakerRights.CompileJobs);
		public bool CanPort => rightsProvider.DreamMakerRights.HasFlag(DreamMakerRights.SetApiValidationPort);
		public bool CanDme => rightsProvider.DreamMakerRights.HasFlag(DreamMakerRights.SetDme);
		public bool CanDmeView => !Refreshing && CanDme && !AutoDetectDme;
		public bool CanSecurity => rightsProvider.DreamMakerRights.HasFlag(DreamMakerRights.SetSecurityLevel);
		public bool CanSecurityView => !Refreshing && CanSecurity;
		public bool CanPortView => !Refreshing && CanPort;

		public EnumCommand<CompilerCommand> Close { get; }
		public EnumCommand<CompilerCommand> Refresh { get; }
		public EnumCommand<CompilerCommand> NextPage { get; }
		public EnumCommand<CompilerCommand> LastPage { get; }
		public EnumCommand<CompilerCommand> Compile { get; }
		public EnumCommand<CompilerCommand> Update { get; }

		readonly PageContextViewModel pageContext;
		readonly IDreamMakerClient dreamMakerClient;
		readonly IInstanceJobSink jobSink;
		readonly IInstanceUserRightsProvider rightsProvider;
		
		readonly Dictionary<int, IReadOnlyList<CompileJobViewModel>> jobPages;

		IReadOnlyList<CompileJob> jobIds;
		int selectedPage;
		int numPages;

		DreamMaker model;

		DreamDaemonSecurity newSecurityLevel;

		string newDme;
		int newPort;
		
		bool refreshing;
		bool autoDetectDme;

		public CompilerViewModel(PageContextViewModel pageContext, IDreamMakerClient dreamMakerClient, IInstanceJobSink jobSink, IInstanceUserRightsProvider rightsProvider)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.dreamMakerClient = dreamMakerClient ?? throw new ArgumentNullException(nameof(dreamMakerClient));
			this.jobSink = jobSink ?? throw new ArgumentNullException(nameof(jobSink));
			this.rightsProvider = rightsProvider ?? throw new ArgumentNullException(nameof(rightsProvider));

			Close = new EnumCommand<CompilerCommand>(CompilerCommand.Close, this);
			Refresh = new EnumCommand<CompilerCommand>(CompilerCommand.Refresh, this);
			Compile = new EnumCommand<CompilerCommand>(CompilerCommand.Compile, this);
			NextPage = new EnumCommand<CompilerCommand>(CompilerCommand.NextPage, this);
			LastPage = new EnumCommand<CompilerCommand>(CompilerCommand.LastPage, this);
			Update = new EnumCommand<CompilerCommand>(CompilerCommand.Update, this);

			rightsProvider.OnUpdated += (a, b) =>
			{
				RecheckCommands();
				this.RaisePropertyChanged(nameof(Icon));
				this.RaisePropertyChanged(nameof(CanDmeView));
				this.RaisePropertyChanged(nameof(CanPortView));
				this.RaisePropertyChanged(nameof(CanRead));
				this.RaisePropertyChanged(nameof(CanSecurity));
				this.RaisePropertyChanged(nameof(CanSecurityView));
				this.RaisePropertyChanged(nameof(CanCompile));
				this.RaisePropertyChanged(nameof(CanGetJobs));
				this.RaisePropertyChanged(nameof(CanPort));
				this.RaisePropertyChanged(nameof(CanDme));
			};

			jobPages = new Dictionary<int, IReadOnlyList<CompileJobViewModel>>();

			newSecurityLevel = DreamDaemonSecurity.Safe;

			async void InitialLoad() => await DoRefresh(default).ConfigureAwait(true);
			InitialLoad();
		}

		void RecheckCommands()
		{
			Refresh.Recheck();
			NextPage.Recheck();
			LastPage.Recheck();
			Compile.Recheck();
			Update.Recheck();
		}

		void ResetFields()
		{
			NewDme = String.Empty;
			NewPort = 0;
			AutoDetectDme = Model?.ProjectName == null;
		}

		async Task DoRefresh(CancellationToken cancellationToken)
		{
			Refreshing = true;
			ResetFields();
			try
			{
				if (!CanRead && !CanGetJobs)
					return;

				async Task AssignModel() => Model = await (CanRead ? dreamMakerClient.Read(cancellationToken) : Task.FromResult<DreamMaker>(null)).ConfigureAwait(true);

				var readTask = AssignModel();

				var jobsTask = CanGetJobs ? dreamMakerClient.GetJobIds(cancellationToken) : Task.FromResult<IReadOnlyList<CompileJob>>(null);
				
				jobIds = await jobsTask.ConfigureAwait(true);
				numPages = (jobIds.Count / JobsPerPage) + (jobIds.Count > JobsPerPage && ((jobIds.Count % JobsPerPage) > 0) ? 1 : 0);
				this.RaisePropertyChanged(nameof(ViewNumPages));
				jobPages.Clear();
				selectedPage = 0;

				await LoadPage(cancellationToken).ConfigureAwait(true);

				await readTask.ConfigureAwait(true);
				ResetFields();
			}
			finally
			{
				Refreshing = false;
			}
		}

		async Task LoadPage(CancellationToken cancellationToken)
		{
			Refreshing = true;
			try
			{
				if (jobPages.ContainsKey(selectedPage))
					return;

				var tasks = new List<Task<CompileJob>>();
				var baseIndex = JobsPerPage * selectedPage;
				var limitIndex = Math.Min(baseIndex + JobsPerPage, jobIds.Count);
				for (var I = baseIndex; I < limitIndex; ++I)
					tasks.Add(dreamMakerClient.GetCompileJob(jobIds[I], cancellationToken));

				await Task.WhenAll(tasks).ConfigureAwait(true);
				jobPages.Add(selectedPage, tasks.Select(x => new CompileJobViewModel(x.Result)).ToList());
			}
			finally
			{
				Refreshing = false;
				this.RaisePropertyChanged(nameof(CurrentPage));
				this.RaisePropertyChanged(nameof(ViewSelectedPage));
			}
		}

		public Task HandleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(CompilerCommand command)
		{
			switch (command)
			{
				case CompilerCommand.Close:
					return true;
				case CompilerCommand.Update:
					return !Refreshing 
						&& (CanPort || CanDme) 
						&& (
						//either a new dme name is set, or the checkbox is different than the model
						(!String.IsNullOrEmpty(NewDme) || (AutoDetectDme ^ (Model?.ProjectName == null))) 
						|| NewPort != 0);
				case CompilerCommand.Compile:
					return !Refreshing && CanCompile;
				case CompilerCommand.LastPage:
					return !Refreshing && CanGetJobs && selectedPage > 0;
				case CompilerCommand.NextPage:
					return !Refreshing && selectedPage < numPages && CanGetJobs;
				case CompilerCommand.Refresh:
					return !Refreshing && (CanGetJobs | CanRead);
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public async Task RunCommand(CompilerCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case CompilerCommand.Close:
					pageContext.ActiveObject = null;
					break;
				case CompilerCommand.Compile:
					Refreshing = true;
					try
					{
						var job = await dreamMakerClient.Compile(cancellationToken).ConfigureAwait(true);
						jobSink.RegisterJob(job);
					}
					finally
					{
						Refreshing = false;
					}
					break;
				case CompilerCommand.LastPage:
					--selectedPage;
					await LoadPage(cancellationToken).ConfigureAwait(true);
					break;
				case CompilerCommand.NextPage:
					++selectedPage;
					await LoadPage(cancellationToken).ConfigureAwait(true);
					break;
				case CompilerCommand.Refresh:
					await DoRefresh(cancellationToken).ConfigureAwait(true);
					break;
				case CompilerCommand.Update:
					Refreshing = true;
					try
					{
						var newModel = new DreamMaker();
						if (CanDme)
							if (!String.IsNullOrEmpty(NewDme))
								newModel.ProjectName = NewDme;
							else if (AutoDetectDme)
								newModel.ProjectName = String.Empty;

						if (CanPort && NewPort != 0)
							newModel.ApiValidationPort = (ushort)NewPort;

						if (CanSecurity && newSecurityLevel != Model.ApiValidationSecurityLevel)
							newModel.ApiValidationSecurityLevel = newSecurityLevel;

						Model = await dreamMakerClient.Update(newModel, cancellationToken).ConfigureAwait(true);
						ResetFields();
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
