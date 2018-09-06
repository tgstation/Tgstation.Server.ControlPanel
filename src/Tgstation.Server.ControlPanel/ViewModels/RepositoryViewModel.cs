using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class RepositoryViewModel : ViewModelBase, ITreeNode, ICommandReceiver<RepositoryViewModel.RepositoryCommand>
	{
		public enum RepositoryCommand
		{
			Close,
			Refresh,
			Clone,
			Delete,
			Update
		}

		public string Title => "Repository";

		public string Icon
		{
			get => icon;
			private set => this.RaiseAndSetIfChanged(ref icon, value);
		}

		public IReadOnlyList<ITreeNode> Children => null;

		public bool IsExpanded { get; set; }

		public Repository Repository
		{
			get => repository;
			set
			{
				this.RaiseAndSetIfChanged(ref repository, value);
				this.RaisePropertyChanged(nameof(Available));
			}
		}

		public bool Available => Repository.Origin != null;

		public string NewOrigin
		{
			get => newOrigin;
			set
			{
				this.RaiseAndSetIfChanged(ref newOrigin, value);
				Clone.Recheck();
			}
		}
		public string NewSha
		{
			get => newSha;
			set => this.RaiseAndSetIfChanged(ref newSha, value);
		}
		public string NewReference
		{
			get => newReference;
			set => this.RaiseAndSetIfChanged(ref newReference, value);
		}
		public string NewCommitterName
		{
			get => newCommitterName;
			set => this.RaiseAndSetIfChanged(ref newCommitterName, value);
		}
		public string NewCommitterEmail
		{
			get => newCommitterEmail;
			set => this.RaiseAndSetIfChanged(ref newCommitterEmail, value);
		}
		public string NewAccessUser
		{
			get => newAccessUser;
			set => this.RaiseAndSetIfChanged(ref newAccessUser, value);
		}
		public string NewAccessToken
		{
			get => newAccessToken;
			set => this.RaiseAndSetIfChanged(ref newAccessToken, value);
		}

		public bool NewUpdateFromOrigin
		{
			get => newUpdateFromOrigin ?? false;
			set
			{
				newUpdateFromOrigin = value ? (bool?)true : null;
				this.RaisePropertyChanged(nameof(newUpdateFromOrigin));
			}
		}
		public bool RemoveCredentials
		{
			get => removeCredentials;
			set => this.RaiseAndSetIfChanged(ref removeCredentials, value);
		}

		public bool NewShowTestMergeCommitters
		{
			get => newShowTestMergeCommitters;
			set => this.RaiseAndSetIfChanged(ref newShowTestMergeCommitters, value);
		}

		public bool NewAutoUpdatesKeepTestMerges
		{
			get => newAutoUpdatesKeepTestMerges;
			set => this.RaiseAndSetIfChanged(ref newAutoUpdatesKeepTestMerges, value);
		}

		public bool NewAutoUpdatesSynchronize
		{
			get => newAutoUpdatesSynchronize;
			set => this.RaiseAndSetIfChanged(ref newAutoUpdatesSynchronize, value);
		}

		public bool CanClone => rightsProvider.RepositoryRights.HasFlag(RepositoryRights.SetOrigin);
		public bool CanDelete => rightsProvider.RepositoryRights.HasFlag(RepositoryRights.Delete);
		public bool CanSetRef => rightsProvider.RepositoryRights.HasFlag(RepositoryRights.SetReference);
		public bool CanSetSha => rightsProvider.RepositoryRights.HasFlag(RepositoryRights.SetSha);
		public bool CanShowTMCommitters => rightsProvider.RepositoryRights.HasFlag(RepositoryRights.ChangeTestMergeCommits);
		public bool CanChangeCommitter => rightsProvider.RepositoryRights.HasFlag(RepositoryRights.ChangeCommitter);
		public bool CanAccess => rightsProvider.RepositoryRights.HasFlag(RepositoryRights.ChangeCredentials);
		public bool CanAutoUpdate => rightsProvider.RepositoryRights.HasFlag(RepositoryRights.ChangeAutoUpdateSettings);
		public bool CanUpdate => rightsProvider.RepositoryRights.HasFlag(RepositoryRights.UpdateBranch);
		public bool CanTestMerge => rightsProvider.RepositoryRights.HasFlag(RepositoryRights.MergePullRequest);

		public bool Error
		{
			get => error;
			set => this.RaiseAndSetIfChanged(ref error, value);
		}

		public bool CloneAvailable
		{
			get => cloneAvailable;
			set => this.RaiseAndSetIfChanged(ref cloneAvailable, value);
		}

		public bool Refreshing
		{
			get => refreshing;
			set => this.RaiseAndSetIfChanged(ref refreshing, value);
		}

		public string ErrorMessage
		{
			get => errorMessage;
			set => this.RaiseAndSetIfChanged(ref errorMessage, value);
		}

		public EnumCommand<RepositoryCommand> Close { get; }
		public EnumCommand<RepositoryCommand> RefreshCommand { get; }
		public EnumCommand<RepositoryCommand> Clone { get; }
		public EnumCommand<RepositoryCommand> Delete { get; }
		public EnumCommand<RepositoryCommand> Update { get; }

		readonly PageContextViewModel pageContext;
		readonly IRepositoryClient repositoryClient;
		readonly IInstanceJobSink jobSink;
		readonly IInstanceUserRightsProvider rightsProvider;
		
		Repository repository;

		string newOrigin;
		string newSha;
		string newReference;
		string newCommitterName;
		string newCommitterEmail;
		string newAccessUser;
		string newAccessToken;

		bool? newUpdateFromOrigin;

		bool removeCredentials;
		bool newShowTestMergeCommitters;
		bool newAutoUpdatesKeepTestMerges;
		bool newAutoUpdatesSynchronize;

		string errorMessage;
		bool error;

		string icon;

		bool refreshing;
		bool cloneAvailable;

		public RepositoryViewModel(PageContextViewModel pageContext, IRepositoryClient repositoryClient, IInstanceJobSink jobSink, IInstanceUserRightsProvider rightsProvider)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.repositoryClient = repositoryClient ?? throw new ArgumentNullException(nameof(repositoryClient));
			this.jobSink = jobSink ?? throw new ArgumentNullException(nameof(jobSink));
			this.rightsProvider = rightsProvider ?? throw new ArgumentNullException(nameof(rightsProvider));

			Close = new EnumCommand<RepositoryCommand>(RepositoryCommand.Close, this);
			RefreshCommand = new EnumCommand<RepositoryCommand>(RepositoryCommand.Refresh, this);
			Clone = new EnumCommand<RepositoryCommand>(RepositoryCommand.Clone, this);
			Delete = new EnumCommand<RepositoryCommand>(RepositoryCommand.Delete, this);
			Update = new EnumCommand<RepositoryCommand>(RepositoryCommand.Update, this);

			rightsProvider.OnUpdated += (a, b) =>
			{
				RecheckCommands();

				this.RaisePropertyChanged(nameof(CanAutoUpdate));
				this.RaisePropertyChanged(nameof(CanChangeCommitter));
				this.RaisePropertyChanged(nameof(CanAccess));
				this.RaisePropertyChanged(nameof(CanShowTMCommitters));
				this.RaisePropertyChanged(nameof(CanTestMerge));
				this.RaisePropertyChanged(nameof(CanSetRef));
				this.RaisePropertyChanged(nameof(CanSetSha));
				this.RaisePropertyChanged(nameof(CanUpdate));
				this.RaisePropertyChanged(nameof(CanDelete));
				this.RaisePropertyChanged(nameof(CanClone));
			};

			async void InitialLoad() => await Refresh(null, null, default).ConfigureAwait(false);
			if (CanRunCommand(RepositoryCommand.Refresh))
				InitialLoad();
		}

		void RecheckCommands()
		{
			RefreshCommand.Recheck();
			Delete.Recheck();
			Clone.Recheck();
			Update.Recheck();
		}

		async Task Refresh(Repository update, bool? cloneOrDelete, CancellationToken cancellationToken)
		{
			Icon = "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png";
			Refreshing = true;
			Error = false;
			ErrorMessage = null;
			CloneAvailable = false;
			RecheckCommands();
			try
			{
				var oldRepo = Repository;
				try
				{
					if (cloneOrDelete.HasValue)
						if (cloneOrDelete.Value)
							Repository = await repositoryClient.Clone(update, cancellationToken).ConfigureAwait(true);
						else
							Repository = await repositoryClient.Delete(cancellationToken).ConfigureAwait(true);
					else if (update != null)
						Repository = await repositoryClient.Update(update, cancellationToken).ConfigureAwait(true);
					else
						Repository = await repositoryClient.Read(cancellationToken).ConfigureAwait(true);
				}
				catch (ClientException e)
				{
					Error = true;
					ErrorMessage = e.Message;
					return;
				}
				if (Repository.ActiveJob != null)
					jobSink.RegisterJob(Repository.ActiveJob);

				NewOrigin = String.Empty;
				NewSha = String.Empty;
				NewReference = String.Empty;
				NewCommitterEmail = String.Empty;
				NewCommitterName = String.Empty;
				NewAccessUser = String.Empty;
				NewAccessToken = String.Empty;

				NewUpdateFromOrigin = false;
				NewAutoUpdatesKeepTestMerges = Repository.AutoUpdatesKeepTestMerges ?? update.AutoUpdatesKeepTestMerges ?? oldRepo.AutoUpdatesKeepTestMerges ?? NewAutoUpdatesKeepTestMerges;
				NewAutoUpdatesSynchronize = Repository.AutoUpdatesSynchronize ?? update.AutoUpdatesSynchronize ?? oldRepo.AutoUpdatesSynchronize ?? NewAutoUpdatesSynchronize;
				NewShowTestMergeCommitters = Repository.ShowTestMergeCommitters ?? update.ShowTestMergeCommitters ?? oldRepo.ShowTestMergeCommitters ?? NewShowTestMergeCommitters;

				CloneAvailable = Repository.Origin == null;
				
				//TODO: Test merges
			}
			finally
			{
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.git.png";
				Refreshing = false;
				RecheckCommands();
			}
		}

		public Task HandleDoubleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(RepositoryCommand command)
		{
			switch (command)
			{
				case RepositoryCommand.Close:
					return true;
				case RepositoryCommand.Refresh:
					return !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.Read);
				case RepositoryCommand.Update:
					return !Refreshing
						&& (CanAutoUpdate | CanChangeCommitter | CanAccess | CanShowTMCommitters | CanTestMerge | CanSetRef | CanSetSha | CanUpdate)
						&& !(!String.IsNullOrWhiteSpace(NewAccessUser) ^ !String.IsNullOrWhiteSpace(NewAccessToken))
						&& !(!String.IsNullOrWhiteSpace(NewSha) && !String.IsNullOrWhiteSpace(NewReference));
				case RepositoryCommand.Delete:
					return !Refreshing && CanDelete;
				case RepositoryCommand.Clone:
					return !Refreshing && CanClone && !String.IsNullOrWhiteSpace(NewOrigin);
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public Task RunCommand(RepositoryCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case RepositoryCommand.Close:
					pageContext.ActiveObject = null;
					return Task.CompletedTask;
				case RepositoryCommand.Refresh:
					return Refresh(null, null, cancellationToken);
				case RepositoryCommand.Update:
					var update = new Repository
					{
						CheckoutSha = String.IsNullOrWhiteSpace(NewSha) || !CanSetSha ? null : NewSha,
						Reference = String.IsNullOrWhiteSpace(NewReference) || !CanSetRef ? null : NewReference,
						UpdateFromOrigin = CanUpdate ? (bool?)NewUpdateFromOrigin : null,

						AccessToken = String.IsNullOrWhiteSpace(NewAccessToken) || !CanAccess ? null : NewAccessToken,
						AccessUser = String.IsNullOrWhiteSpace(NewAccessUser) || !CanAccess ? null : NewAccessUser,

						AutoUpdatesKeepTestMerges = CanAutoUpdate ? (bool?)NewAutoUpdatesKeepTestMerges : null,
						AutoUpdatesSynchronize = CanAutoUpdate ? (bool?)NewAutoUpdatesSynchronize : null,

						ShowTestMergeCommitters = CanShowTMCommitters ? (bool?)NewShowTestMergeCommitters : null,

						CommitterEmail = CanChangeCommitter ? NewCommitterEmail : null,
						CommitterName = CanChangeCommitter ? NewCommitterName : null
					};
					return Refresh(update, null, cancellationToken);
				case RepositoryCommand.Delete:
					return Refresh(null, false, cancellationToken);
				case RepositoryCommand.Clone:
					var clone = new Repository
					{
						Origin = NewOrigin,
						Reference = String.IsNullOrWhiteSpace(NewReference) ? null : NewReference,
						AccessToken = String.IsNullOrWhiteSpace(NewAccessToken) || !CanAccess ? null : NewAccessToken,
						AccessUser = String.IsNullOrWhiteSpace(NewAccessUser) || !CanAccess ? null : NewAccessUser,
					};
					return Refresh(clone, true, cancellationToken);
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}
