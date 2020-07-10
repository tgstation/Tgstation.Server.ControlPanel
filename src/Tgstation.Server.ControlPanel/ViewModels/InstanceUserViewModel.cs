using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class InstanceUserViewModel : ViewModelBase, ITreeNode, IInstanceUserRightsProvider, ICommandReceiver<InstanceUserViewModel.InstanceUserCommand>
	{
		public enum InstanceUserCommand
		{
			Close,
			Refresh,
			Save,
			Delete
		}

		public string Title => rightsProvider == this ? "Current User" : displayName;

		public string Icon => "resm:Tgstation.Server.ControlPanel.Assets.user.png";

		public string DeleteText => confirmingDelete ? "Confirm?" : "Delete";

		public bool IsExpanded { get; set; }

		public IReadOnlyList<ITreeNode> Children => null;

		public long Id => instanceUser.UserId.Value;

		public InstanceUserRights InstanceUserRights => instanceUser.InstanceUserRights.Value;
		public RepositoryRights RepositoryRights => instanceUser.RepositoryRights.Value;
		public ByondRights ByondRights => instanceUser.ByondRights.Value;
		public DreamMakerRights DreamMakerRights => instanceUser.DreamMakerRights.Value;
		public DreamDaemonRights DreamDaemonRights => instanceUser.DreamDaemonRights.Value;
		public ChatBotRights ChatBotRights => instanceUser.ChatBotRights.Value;
		public ConfigurationRights ConfigurationRights => instanceUser.ConfigurationRights.Value;
		public AdministrationRights AdministrationRights => userRightsProvider.AdministrationRights;
		public InstanceManagerRights InstanceManagerRights => userRightsProvider.InstanceManagerRights;

		public bool UserRead
		{
			get => newInstanceUserRights.HasFlag(InstanceUserRights.ReadUsers);
			set
			{
				var right = InstanceUserRights.ReadUsers;
				if (value)
					newInstanceUserRights |= right;
				else
					newInstanceUserRights &= ~right;
			}
		}
		public bool UserWrite
		{
			get => newInstanceUserRights.HasFlag(InstanceUserRights.WriteUsers);
			set
			{
				var right = InstanceUserRights.WriteUsers;
				if (value)
					newInstanceUserRights |= right;
				else
					newInstanceUserRights &= ~right;
			}
		}
		public bool UserCreate
		{
			get => newInstanceUserRights.HasFlag(InstanceUserRights.CreateUsers);
			set
			{
				var right = InstanceUserRights.CreateUsers;
				if (value)
					newInstanceUserRights |= right;
				else
					newInstanceUserRights &= ~right;
			}
		}

		public bool ByondRead
		{
			get => newByondRights.HasFlag(ByondRights.ReadActive);
			set
			{
				var right = ByondRights.ReadActive;
				if (value)
					newByondRights |= right;
				else
					newByondRights &= ~right;
			}
		}
		public bool ByondList
		{
			get => newByondRights.HasFlag(ByondRights.ListInstalled);
			set
			{
				var right = ByondRights.ListInstalled;
				if (value)
					newByondRights |= right;
				else
					newByondRights &= ~right;
			}
		}
		public bool ByondChange
		{
			get => newByondRights.HasFlag(ByondRights.InstallOfficialOrChangeActiveVersion);
			set
			{
				var right = ByondRights.InstallOfficialOrChangeActiveVersion;
				if (value)
					newByondRights |= right;
				else
					newByondRights &= ~right;
			}
		}
		public bool ByondCancel
		{
			get => newByondRights.HasFlag(ByondRights.CancelInstall);
			set
			{
				var right = ByondRights.CancelInstall;
				if (value)
					newByondRights |= right;
				else
					newByondRights &= ~right;
			}
		}
		public bool ByondUpload
		{
			get => newByondRights.HasFlag(ByondRights.InstallCustomVersion);
			set
			{
				var right = ByondRights.InstallCustomVersion;
				if (value)
					newByondRights |= right;
				else
					newByondRights &= ~right;
			}
		}

		public bool RepoRead
		{
			get => newRepositoryRights.HasFlag(RepositoryRights.Read);
			set
			{
				var right = RepositoryRights.Read;
				if (value)
					newRepositoryRights |= right;
				else
					newRepositoryRights &= ~right;
			}
		}
		public bool RepoOrigin
		{
			get => newRepositoryRights.HasFlag(RepositoryRights.SetOrigin);
			set
			{
				var right = RepositoryRights.SetOrigin;
				if (value)
					newRepositoryRights |= right;
				else
					newRepositoryRights &= ~right;
			}
		}
		public bool RepoSha
		{
			get => newRepositoryRights.HasFlag(RepositoryRights.SetSha);
			set
			{
				var right = RepositoryRights.SetSha;
				if (value)
					newRepositoryRights |= right;
				else
					newRepositoryRights &= ~right;
			}
		}
		public bool RepoTestMerge
		{
			get => newRepositoryRights.HasFlag(RepositoryRights.MergePullRequest);
			set
			{
				var right = RepositoryRights.MergePullRequest;
				if (value)
					newRepositoryRights |= right;
				else
					newRepositoryRights &= ~right;
			}
		}
		public bool RepoReset
		{
			get => newRepositoryRights.HasFlag(RepositoryRights.UpdateBranch);
			set
			{
				var right = RepositoryRights.UpdateBranch;
				if (value)
					newRepositoryRights |= right;
				else
					newRepositoryRights &= ~right;
			}
		}
		public bool RepoCommitter
		{
			get => newRepositoryRights.HasFlag(RepositoryRights.ChangeCommitter);
			set
			{
				var right = RepositoryRights.ChangeCommitter;
				if (value)
					newRepositoryRights |= right;
				else
					newRepositoryRights &= ~right;
			}
		}
		public bool RepoTMCommits
		{
			get => newRepositoryRights.HasFlag(RepositoryRights.ChangeTestMergeCommits);
			set
			{
				var right = RepositoryRights.ChangeTestMergeCommits;
				if (value)
					newRepositoryRights |= right;
				else
					newRepositoryRights &= ~right;
			}
		}
		public bool RepoCreds
		{
			get => newRepositoryRights.HasFlag(RepositoryRights.ChangeCredentials);
			set
			{
				var right = RepositoryRights.ChangeCredentials;
				if (value)
					newRepositoryRights |= right;
				else
					newRepositoryRights &= ~right;
			}
		}
		public bool RepoRef
		{
			get => newRepositoryRights.HasFlag(RepositoryRights.SetReference);
			set
			{
				var right = RepositoryRights.SetReference;
				if (value)
					newRepositoryRights |= right;
				else
					newRepositoryRights &= ~right;
			}
		}
		public bool RepoAuto
		{
			get => newRepositoryRights.HasFlag(RepositoryRights.ChangeAutoUpdateSettings);
			set
			{
				var right = RepositoryRights.ChangeAutoUpdateSettings;
				if (value)
					newRepositoryRights |= right;
				else
					newRepositoryRights &= ~right;
			}
		}
		public bool RepoDelete
		{
			get => newRepositoryRights.HasFlag(RepositoryRights.Delete);
			set
			{
				var right = RepositoryRights.Delete;
				if (value)
					newRepositoryRights |= right;
				else
					newRepositoryRights &= ~right;
			}
		}
		public bool RepoCancelClone
		{
			get => newRepositoryRights.HasFlag(RepositoryRights.CancelClone);
			set
			{
				var right = RepositoryRights.CancelClone;
				if (value)
					newRepositoryRights |= right;
				else
					newRepositoryRights &= ~right;
			}
		}
		public bool RepoCancelUpdate
		{
			get => newRepositoryRights.HasFlag(RepositoryRights.CancelPendingChanges);
			set
			{
				var right = RepositoryRights.CancelPendingChanges;
				if (value)
					newRepositoryRights |= right;
				else
					newRepositoryRights &= ~right;
			}
		}

		public bool CompRead
		{
			get => newDreamMakerRights.HasFlag(DreamMakerRights.Read);
			set
			{
				var right = DreamMakerRights.Read;
				if (value)
					newDreamMakerRights |= right;
				else
					newDreamMakerRights &= ~right;
			}
		}
		public bool CompStart
		{
			get => newDreamMakerRights.HasFlag(DreamMakerRights.Compile);
			set
			{
				var right = DreamMakerRights.Compile;
				if (value)
					newDreamMakerRights |= right;
				else
					newDreamMakerRights &= ~right;
			}
		}
		public bool CompCancel
		{
			get => newDreamMakerRights.HasFlag(DreamMakerRights.CancelCompile);
			set
			{
				var right = DreamMakerRights.CancelCompile;
				if (value)
					newDreamMakerRights |= right;
				else
					newDreamMakerRights &= ~right;
			}
		}
		public bool CompDme
		{
			get => newDreamMakerRights.HasFlag(DreamMakerRights.SetDme);
			set
			{
				var right = DreamMakerRights.SetDme;
				if (value)
					newDreamMakerRights |= right;
				else
					newDreamMakerRights &= ~right;
			}
		}
		public bool CompVali
		{
			get => newDreamMakerRights.HasFlag(DreamMakerRights.SetApiValidationPort);
			set
			{
				var right = DreamMakerRights.SetApiValidationPort;
				if (value)
					newDreamMakerRights |= right;
				else
					newDreamMakerRights &= ~right;
			}
		}
		public bool CompList
		{
			get => newDreamMakerRights.HasFlag(DreamMakerRights.CompileJobs);
			set
			{
				var right = DreamMakerRights.CompileJobs;
				if (value)
					newDreamMakerRights |= right;
				else
					newDreamMakerRights &= ~right;
			}
		}
		public bool CompSec
		{
			get => newDreamMakerRights.HasFlag(DreamMakerRights.SetSecurityLevel);
			set
			{
				var right = DreamMakerRights.SetSecurityLevel;
				if (value)
					newDreamMakerRights |= right;
				else
					newDreamMakerRights &= ~right;
			}
		}
		public bool CompReq
		{
			get => newDreamMakerRights.HasFlag(DreamMakerRights.SetApiValidationRequirement);
			set
			{
				var right = DreamMakerRights.SetSecurityLevel;
				if (value)
					newDreamMakerRights |= right;
				else
					newDreamMakerRights &= ~right;
			}
		}

		public bool DDRead
		{
			get => newDreamDaemonRights.HasFlag(DreamDaemonRights.ReadRevision);
			set
			{
				var right = DreamDaemonRights.ReadRevision;
				if (value)
					newDreamDaemonRights |= right;
				else
					newDreamDaemonRights &= ~right;
			}
		}
		public bool DDPort
		{
			get => newDreamDaemonRights.HasFlag(DreamDaemonRights.SetPort);
			set
			{
				var right = DreamDaemonRights.SetPort;
				if (value)
					newDreamDaemonRights |= right;
				else
					newDreamDaemonRights &= ~right;
			}
		}
		public bool DDAuto
		{
			get => newDreamDaemonRights.HasFlag(DreamDaemonRights.SetAutoStart);
			set
			{
				var right = DreamDaemonRights.SetAutoStart;
				if (value)
					newDreamDaemonRights |= right;
				else
					newDreamDaemonRights &= ~right;
			}
		}
		public bool DDSec
		{
			get => newDreamDaemonRights.HasFlag(DreamDaemonRights.SetSecurity);
			set
			{
				var right = DreamDaemonRights.SetSecurity;
				if (value)
					newDreamDaemonRights |= right;
				else
					newDreamDaemonRights &= ~right;
			}
		}
		public bool DDMeta
		{
			get => newDreamDaemonRights.HasFlag(DreamDaemonRights.ReadMetadata);
			set
			{
				var right = DreamDaemonRights.ReadMetadata;
				if (value)
					newDreamDaemonRights |= right;
				else
					newDreamDaemonRights &= ~right;
			}
		}
		public bool DDWeb
		{
			get => newDreamDaemonRights.HasFlag(DreamDaemonRights.SetWebClient);
			set
			{
				var right = DreamDaemonRights.SetWebClient;
				if (value)
					newDreamDaemonRights |= right;
				else
					newDreamDaemonRights &= ~right;
			}
		}
		public bool DDSoftR
		{
			get => newDreamDaemonRights.HasFlag(DreamDaemonRights.SoftRestart);
			set
			{
				var right = DreamDaemonRights.SoftRestart;
				if (value)
					newDreamDaemonRights |= right;
				else
					newDreamDaemonRights &= ~right;
			}
		}
		public bool DDSoftT
		{
			get => newDreamDaemonRights.HasFlag(DreamDaemonRights.SoftShutdown);
			set
			{
				var right = DreamDaemonRights.SoftShutdown;
				if (value)
					newDreamDaemonRights |= right;
				else
					newDreamDaemonRights &= ~right;
			}
		}
		public bool DDRes
		{
			get => newDreamDaemonRights.HasFlag(DreamDaemonRights.Restart);
			set
			{
				var right = DreamDaemonRights.Restart;
				if (value)
					newDreamDaemonRights |= right;
				else
					newDreamDaemonRights &= ~right;
			}
		}
		public bool DDTerm
		{
			get => newDreamDaemonRights.HasFlag(DreamDaemonRights.Shutdown);
			set
			{
				var right = DreamDaemonRights.Shutdown;
				if (value)
					newDreamDaemonRights |= right;
				else
					newDreamDaemonRights &= ~right;
			}
		}
		public bool DDStart
		{
			get => newDreamDaemonRights.HasFlag(DreamDaemonRights.Start);
			set
			{
				var right = DreamDaemonRights.Start;
				if (value)
					newDreamDaemonRights |= right;
				else
					newDreamDaemonRights &= ~right;
			}
		}

		public bool DDTime
		{
			get => newDreamDaemonRights.HasFlag(DreamDaemonRights.SetStartupTimeout);
			set
			{
				var right = DreamDaemonRights.SetStartupTimeout;
				if (value)
					newDreamDaemonRights |= right;
				else
					newDreamDaemonRights &= ~right;
			}
		}

		public bool DDHeart
		{
			get => newDreamDaemonRights.HasFlag(DreamDaemonRights.SetHeartbeatInterval);
			set
			{
				var right = DreamDaemonRights.SetHeartbeatInterval;
				if (value)
					newDreamDaemonRights |= right;
				else
					newDreamDaemonRights &= ~right;
			}
		}

		public bool DDDump
		{
			get => newDreamDaemonRights.HasFlag(DreamDaemonRights.CreateDump);
			set
			{
				var right = DreamDaemonRights.CreateDump;
				if (value)
					newDreamDaemonRights |= right;
				else
					newDreamDaemonRights &= ~right;
			}
		}

		public bool DDTopicTimeout
		{
			get => newDreamDaemonRights.HasFlag(DreamDaemonRights.SetTopicTimeout);
			set
			{
				var right = DreamDaemonRights.SetTopicTimeout;
				if (value)
					newDreamDaemonRights |= right;
				else
					newDreamDaemonRights &= ~right;
			}
		}

		public bool ChatEnable
		{
			get => newChatBotRights.HasFlag(ChatBotRights.WriteEnabled);
			set
			{
				var right = ChatBotRights.WriteEnabled;
				if (value)
					newChatBotRights |= right;
				else
					newChatBotRights &= ~right;
			}
		}
		public bool ChatProvider
		{
			get => newChatBotRights.HasFlag(ChatBotRights.WriteProvider);
			set
			{
				var right = ChatBotRights.WriteProvider;
				if (value)
					newChatBotRights |= right;
				else
					newChatBotRights &= ~right;
			}
		}
		public bool ChatChannels
		{
			get => newChatBotRights.HasFlag(ChatBotRights.WriteChannels);
			set
			{
				var right = ChatBotRights.WriteChannels;
				if (value)
					newChatBotRights |= right;
				else
					newChatBotRights &= ~right;
			}
		}
		public bool ChatReadString
		{
			get => newChatBotRights.HasFlag(ChatBotRights.ReadConnectionString);
			set
			{
				var right = ChatBotRights.ReadConnectionString;
				if (value)
					newChatBotRights |= right;
				else
					newChatBotRights &= ~right;
			}
		}
		public bool ChatWriteString
		{
			get => newChatBotRights.HasFlag(ChatBotRights.WriteConnectionString);
			set
			{
				var right = ChatBotRights.WriteConnectionString;
				if (value)
					newChatBotRights |= right;
				else
					newChatBotRights &= ~right;
			}
		}
		public bool ChatRead
		{
			get => newChatBotRights.HasFlag(ChatBotRights.Read);
			set
			{
				var right = ChatBotRights.Read;
				if (value)
					newChatBotRights |= right;
				else
					newChatBotRights &= ~right;
			}
		}
		public bool ChatName
		{
			get => newChatBotRights.HasFlag(ChatBotRights.WriteName);
			set
			{
				var right = ChatBotRights.WriteName;
				if (value)
					newChatBotRights |= right;
				else
					newChatBotRights &= ~right;
			}
		}
		public bool ChatCreate
		{
			get => newChatBotRights.HasFlag(ChatBotRights.Create);
			set
			{
				var right = ChatBotRights.Create;
				if (value)
					newChatBotRights |= right;
				else
					newChatBotRights &= ~right;
			}
		}
		public bool ChatDelete
		{
			get => newChatBotRights.HasFlag(ChatBotRights.Delete);
			set
			{
				var right = ChatBotRights.Delete;
				if (value)
					newChatBotRights |= right;
				else
					newChatBotRights &= ~right;
			}
		}
		public bool ChatChannelLimit
		{
			get => newChatBotRights.HasFlag(ChatBotRights.WriteChannelLimit);
			set
			{
				var right = ChatBotRights.WriteChannelLimit;
				if (value)
					newChatBotRights |= right;
				else
					newChatBotRights &= ~right;
			}
		}

		public bool StaticRead
		{
			get => newConfigurationRights.HasFlag(ConfigurationRights.Read);
			set
			{
				var right = ConfigurationRights.Read;
				if (value)
					newConfigurationRights |= right;
				else
					newConfigurationRights &= ~right;
			}
		}
		public bool StaticWrite
		{
			get => newConfigurationRights.HasFlag(ConfigurationRights.Write);
			set
			{
				var right = ConfigurationRights.Write;
				if (value)
					newConfigurationRights |= right;
				else
					newConfigurationRights &= ~right;
			}
		}
		public bool StaticList
		{
			get => newConfigurationRights.HasFlag(ConfigurationRights.List);
			set
			{
				var right = ConfigurationRights.List;
				if (value)
					newConfigurationRights |= right;
				else
					newConfigurationRights &= ~right;
			}
		}
		public bool StaticDelete
		{
			get => newConfigurationRights.HasFlag(ConfigurationRights.Delete);
			set
			{
				var right = ConfigurationRights.Delete;
				if (value)
					newConfigurationRights |= right;
				else
					newConfigurationRights &= ~right;
			}
		}

		public EnumCommand<InstanceUserCommand> Close { get; }
		public EnumCommand<InstanceUserCommand> RefreshCommand { get; }
		public EnumCommand<InstanceUserCommand> Save { get; }
		public EnumCommand<InstanceUserCommand> Delete { get; }

		readonly PageContextViewModel pageContext;
		readonly InstanceViewModel instanceViewModel;
		readonly IUserRightsProvider userRightsProvider;
		readonly IInstanceUserClient instanceUserClient;
		readonly IInstanceUserRightsProvider rightsProvider;
		readonly InstanceUserRootViewModel instanceUserRootViewModel;
		readonly string displayName;

		InstanceUser instanceUser;

		InstanceUserRights newInstanceUserRights;
		RepositoryRights newRepositoryRights;
		ByondRights newByondRights;
		DreamMakerRights newDreamMakerRights;
		DreamDaemonRights newDreamDaemonRights;
		ChatBotRights newChatBotRights;
		ConfigurationRights newConfigurationRights;

		bool loading;
		bool confirmingDelete;

		public event EventHandler OnUpdated;

		public InstanceUserViewModel(PageContextViewModel pageContext, InstanceViewModel instanceViewModel, IUserRightsProvider userRightsProvider, IInstanceUserClient instanceUserClient, InstanceUser instanceUser, string displayName, IInstanceUserRightsProvider rightsProvider, InstanceUserRootViewModel instanceUserRootViewModel)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.instanceViewModel = instanceViewModel ?? throw new ArgumentNullException(nameof(instanceViewModel));
			this.userRightsProvider = userRightsProvider ?? throw new ArgumentNullException(nameof(userRightsProvider));
			this.instanceUserClient = instanceUserClient ?? throw new ArgumentNullException(nameof(instanceUserClient));
			this.instanceUser = instanceUser ?? throw new ArgumentNullException(nameof(instanceUser));
			this.instanceUserRootViewModel = instanceUserRootViewModel;
			this.displayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
			this.rightsProvider = rightsProvider ?? this;

			userRightsProvider.OnUpdated += (a, b) => OnUpdated?.Invoke(this, new EventArgs());

			PostLoad();

			Close = new EnumCommand<InstanceUserCommand>(InstanceUserCommand.Close, this);
			RefreshCommand = new EnumCommand<InstanceUserCommand>(InstanceUserCommand.Refresh, this);
			Save = new EnumCommand<InstanceUserCommand>(InstanceUserCommand.Save, this);
			Delete = new EnumCommand<InstanceUserCommand>(InstanceUserCommand.Delete, this);

			this.rightsProvider.OnUpdated += (a, b) =>
			{
				Save.Recheck();
				Delete.Recheck();
			};
		}

		void PostLoad()
		{
			newInstanceUserRights = instanceUser.InstanceUserRights.Value;
			newByondRights = instanceUser.ByondRights.Value;
			newChatBotRights = instanceUser.ChatBotRights.Value;
			newRepositoryRights = instanceUser.RepositoryRights.Value;
			newDreamDaemonRights = instanceUser.DreamDaemonRights.Value;
			newDreamMakerRights = instanceUser.DreamMakerRights.Value;
			newConfigurationRights = instanceUser.ConfigurationRights.Value;

			using (DelayChangeNotifications())
			{
				this.RaisePropertyChanged(nameof(UserRead));
				this.RaisePropertyChanged(nameof(UserWrite));
				this.RaisePropertyChanged(nameof(UserCreate));

				this.RaisePropertyChanged(nameof(ByondRead));
				this.RaisePropertyChanged(nameof(ByondList));
				this.RaisePropertyChanged(nameof(ByondChange));
				this.RaisePropertyChanged(nameof(ByondCancel));

				this.RaisePropertyChanged(nameof(RepoRead));
				this.RaisePropertyChanged(nameof(RepoOrigin));
				this.RaisePropertyChanged(nameof(RepoSha));
				this.RaisePropertyChanged(nameof(RepoTestMerge));
				this.RaisePropertyChanged(nameof(RepoReset));
				this.RaisePropertyChanged(nameof(RepoCommitter));
				this.RaisePropertyChanged(nameof(RepoTMCommits));
				this.RaisePropertyChanged(nameof(RepoCreds));
				this.RaisePropertyChanged(nameof(RepoRef));
				this.RaisePropertyChanged(nameof(RepoAuto));
				this.RaisePropertyChanged(nameof(RepoDelete));
				this.RaisePropertyChanged(nameof(RepoCancelClone));
				this.RaisePropertyChanged(nameof(RepoCancelUpdate));

				this.RaisePropertyChanged(nameof(CompRead));
				this.RaisePropertyChanged(nameof(CompStart));
				this.RaisePropertyChanged(nameof(CompCancel));
				this.RaisePropertyChanged(nameof(CompDme));
				this.RaisePropertyChanged(nameof(CompVali));
				this.RaisePropertyChanged(nameof(CompList));
				this.RaisePropertyChanged(nameof(CompSec));

				this.RaisePropertyChanged(nameof(DDRead));
				this.RaisePropertyChanged(nameof(DDPort));
				this.RaisePropertyChanged(nameof(DDAuto));
				this.RaisePropertyChanged(nameof(DDSec));
				this.RaisePropertyChanged(nameof(DDMeta));
				this.RaisePropertyChanged(nameof(DDWeb));
				this.RaisePropertyChanged(nameof(DDSoftR));
				this.RaisePropertyChanged(nameof(DDSoftT));
				this.RaisePropertyChanged(nameof(DDRes));
				this.RaisePropertyChanged(nameof(DDTerm));
				this.RaisePropertyChanged(nameof(DDStart));
				this.RaisePropertyChanged(nameof(DDTime));

				this.RaisePropertyChanged(nameof(ChatEnable));
				this.RaisePropertyChanged(nameof(ChatProvider));
				this.RaisePropertyChanged(nameof(ChatChannels));
				this.RaisePropertyChanged(nameof(ChatReadString));
				this.RaisePropertyChanged(nameof(ChatWriteString));
				this.RaisePropertyChanged(nameof(ChatRead));
				this.RaisePropertyChanged(nameof(ChatName));
				this.RaisePropertyChanged(nameof(ChatCreate));
				this.RaisePropertyChanged(nameof(ChatDelete));
				this.RaisePropertyChanged(nameof(ChatChannelLimit));

				this.RaisePropertyChanged(nameof(StaticRead));
				this.RaisePropertyChanged(nameof(StaticWrite));
				this.RaisePropertyChanged(nameof(StaticList));
			}
		}

		async Task Refresh(CancellationToken cancellationToken)
		{
			lock (this)
			{
				if (loading)
					return;
				loading = true;
			}
			RefreshCommand.Recheck();
			Save.Recheck();
			Delete.Recheck();

			try
			{
				instanceUser = await instanceUserClient.GetId(instanceUser, cancellationToken).ConfigureAwait(true);

				PostLoad();
			}
			finally
			{
				loading = false;
				RefreshCommand.Recheck();
				Save.Recheck();
				Delete.Recheck();
			}
		}

		public Task HandleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(InstanceUserCommand command)
		{
			switch (command)
			{
				case InstanceUserCommand.Close:
					return true;
				case InstanceUserCommand.Refresh:
					return !loading;
				case InstanceUserCommand.Save:
				case InstanceUserCommand.Delete:
					return rightsProvider.InstanceUserRights.HasFlag(InstanceUserRights.WriteUsers) && !loading;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public async Task RunCommand(InstanceUserCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case InstanceUserCommand.Close:
					pageContext.ActiveObject = null;
					break;
				case InstanceUserCommand.Refresh:
					await Refresh(cancellationToken).ConfigureAwait(false);
					IsExpanded = true;
					this.RaisePropertyChanged(nameof(IsExpanded));
					break;
				case InstanceUserCommand.Save:
					var update = new InstanceUser
					{
						UserId = instanceUser.UserId,
						ByondRights = newByondRights,
						ChatBotRights = newChatBotRights,
						ConfigurationRights = newConfigurationRights,
						DreamDaemonRights = newDreamDaemonRights,
						DreamMakerRights = newDreamMakerRights,
						InstanceUserRights = newInstanceUserRights,
						RepositoryRights = newRepositoryRights
					};
					loading = true;
					RefreshCommand.Recheck();
					Save.Recheck();
					Delete.Recheck();
					try
					{
						instanceUser = await instanceUserClient.Update(update, cancellationToken).ConfigureAwait(true);
						PostLoad();
					}
					finally
					{
						loading = false;
						RefreshCommand.Recheck();
						Save.Recheck();
						Delete.Recheck();
					}
					break;
				case InstanceUserCommand.Delete:
					if (!confirmingDelete)
					{
						confirmingDelete = true;
						this.RaisePropertyChanged(nameof(DeleteText));

						async void ResetConfirm()
						{
							await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(true);
							confirmingDelete = false;
							this.RaisePropertyChanged(nameof(DeleteText));
						}
						ResetConfirm();
						return;
					}

					await instanceUserClient.Delete(instanceUser, cancellationToken).ConfigureAwait(true);
					pageContext.ActiveObject = null;
					if (rightsProvider == this)
						await instanceViewModel.Refresh(cancellationToken).ConfigureAwait(true);
					else
						await instanceUserRootViewModel.Refresh(cancellationToken).ConfigureAwait(true);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}