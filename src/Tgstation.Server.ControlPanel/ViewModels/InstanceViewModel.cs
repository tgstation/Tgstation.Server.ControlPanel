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
	sealed class InstanceViewModel : ViewModelBase, ITreeNode, ICommandReceiver<InstanceViewModel.InstanceCommand>
	{
		public enum InstanceCommand
		{
			Close,
			Save,
			Delete,
			FixPerms
		}

		public string Title => Instance.Name;

		public bool IsExpanded { get; set; }

		public string Icon => instance.Online.Value ? (ddRunning ? "resm:Tgstation.Server.ControlPanel.Assets.database_on.jpg" : "resm:Tgstation.Server.ControlPanel.Assets.database.png") : "resm:Tgstation.Server.ControlPanel.Assets.database_down.jpg";


		public string DeleteText => deleteConfirming ? "Confirm?" : "Detach";

		public IReadOnlyList<ITreeNode> Children
		{
			get => children;
			set => this.RaiseAndSetIfChanged(ref children, value);
		}

		public Instance Instance
		{
			get => instance;
			set
			{
				this.RaiseAndSetIfChanged(ref instance, value);
				this.RaisePropertyChanged(nameof(Icon));
				this.RaisePropertyChanged(nameof(Title));
			}
		}

		public bool CanOnline => userRightsProvider.InstanceManagerRights.HasFlag(InstanceManagerRights.SetOnline);
		public bool CanRename => userRightsProvider.InstanceManagerRights.HasFlag(InstanceManagerRights.Rename);
		public bool CanRelocate => userRightsProvider.InstanceManagerRights.HasFlag(InstanceManagerRights.Relocate);
		public bool CanAutoUpdate => userRightsProvider.InstanceManagerRights.HasFlag(InstanceManagerRights.SetAutoUpdate);
		public bool CanConfig => userRightsProvider.InstanceManagerRights.HasFlag(InstanceManagerRights.SetConfiguration);
		public bool CanDelete => userRightsProvider.InstanceManagerRights.HasFlag(InstanceManagerRights.Delete);

		public bool Enabled
		{
			get => enabled;
			set => this.RaiseAndSetIfChanged(ref enabled, value);
		}
		public string NewName
		{
			get => newName;
			set => this.RaiseAndSetIfChanged(ref newName, value);
		}
		public string NewPath
		{
			get => newPath;
			set => this.RaiseAndSetIfChanged(ref newPath, value);
		}
		public ConfigurationType ConfigMode
		{
			get => configType;
			set => this.RaiseAndSetIfChanged(ref configType, value);
		}
		public uint AutoUpdateInterval
		{
			get => autoUpdateInterval;
			set => this.RaiseAndSetIfChanged(ref autoUpdateInterval, value);
		}

		public EnumCommand<InstanceCommand> Close { get; }
		public EnumCommand<InstanceCommand> Save { get; }
		public EnumCommand<InstanceCommand> Delete { get; }
		public EnumCommand<InstanceCommand> FixPerms { get; }

		public AdministrationRights AdministrationRights => userRightsProvider.AdministrationRights;

		public InstanceManagerRights InstanceManagerRights => userRightsProvider.InstanceManagerRights;

		readonly IInstanceManagerClient instanceManagerClient;
		readonly IInstanceClient instanceClient;
		readonly PageContextViewModel pageContext;
		readonly IUserRightsProvider userRightsProvider;
		readonly InstanceRootViewModel instanceRootViewModel;
		readonly IUserProvider userProvider;

		IInstanceJobSink instanceJobSink;

		IReadOnlyList<ITreeNode> children;
		Instance instance;
		bool ddRunning;

		bool enabled;
		string newName;
		string newPath;
		ConfigurationType configType;
		uint autoUpdateInterval;

		bool deleteConfirming;
		bool loading;

		InstanceUser instanceUser;

		public InstanceViewModel(IInstanceManagerClient instanceManagerClient, PageContextViewModel pageContext, Instance instance, IUserRightsProvider userRightsProvider, InstanceRootViewModel instanceRootViewModel, IUserProvider userProvider, IServerJobSink serverJobSink)
		{
			this.instanceManagerClient = instanceManagerClient ?? throw new ArgumentNullException(nameof(instanceManagerClient));
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			Instance = instance ?? throw new ArgumentNullException(nameof(instance));
			this.userRightsProvider = userRightsProvider ?? throw new ArgumentNullException(nameof(userRightsProvider));
			this.instanceRootViewModel = instanceRootViewModel ?? throw new ArgumentNullException(nameof(instanceRootViewModel));
			this.userProvider = userProvider ?? throw new ArgumentNullException(nameof(userProvider));

			instanceClient = instanceManagerClient.CreateClient(instance);

			SafeLoad(serverJobSink);

			Close = new EnumCommand<InstanceCommand>(InstanceCommand.Close, this);
			Save = new EnumCommand<InstanceCommand>(InstanceCommand.Save, this);
			Delete = new EnumCommand<InstanceCommand>(InstanceCommand.Delete, this);
			FixPerms = new EnumCommand<InstanceCommand>(InstanceCommand.FixPerms, this);

			userRightsProvider.OnUpdated += (a, b) =>
			{
				using (DelayChangeNotifications())
				{
					this.RaisePropertyChanged(nameof(CanDelete));
					this.RaisePropertyChanged(nameof(CanConfig));
					this.RaisePropertyChanged(nameof(CanAutoUpdate));
					this.RaisePropertyChanged(nameof(CanRelocate));
					this.RaisePropertyChanged(nameof(CanRename));
					this.RaisePropertyChanged(nameof(CanOnline));
					Delete.Recheck();
					Save.Recheck();
					FixPerms.Recheck();
				}
			};
		}

		async void SafeLoad(IServerJobSink serverJobSink)
		{
			if(serverJobSink != null)
				instanceJobSink = await serverJobSink?.GetSinkForInstance(instanceClient, default) ?? throw new ArgumentNullException(nameof(serverJobSink));
			await PostRefresh(default).ConfigureAwait(true);
		}
	
		public void SetDDRunning(bool yes)
		{
			ddRunning = yes;
			this.RaisePropertyChanged(nameof(Icon));
		}

		async Task PostRefresh(CancellationToken cancellationToken)
		{
			using (DelayChangeNotifications())
			{
				Enabled = Instance.Online.Value;
				ConfigMode = Instance.ConfigurationType.Value;
				AutoUpdateInterval = Instance.AutoUpdateInterval.Value;
				NewName = String.Empty;
				NewPath = String.Empty;

				if (!Enabled)
					return;

				Children = new List<ITreeNode>
				{
					new BasicNode
					{
						Title = "Loading",
						Icon = "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png"
					}
				};
			}

			try
			{
				instanceUser = await instanceClient.Users.Read(cancellationToken).ConfigureAwait(false);
			}
			catch (InsufficientPermissionsException)
			{
				//reeee
				Children = null;
				return;
			}

			var instanceUserTreeNode = new InstanceUserViewModel(pageContext, this, userRightsProvider, instanceClient.Users, instanceUser, InstanceUserRootViewModel.GetDisplayNameForInstanceUser(userProvider, instanceUser), null, null);

			instanceUserTreeNode.OnUpdated += (a, b) => SafeLoad(null);

			var canReadDD = instanceUser.DreamDaemonRights.Value.HasFlag(DreamDaemonRights.ReadMetadata);
			ITreeNode ddNode;
			if (canReadDD)
				ddNode = new BasicNode
				{
					Title = "TODO: DreamDaemon",
					Icon = "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png"
				};
			else
			{
				ddNode = new BasicNode
				{
					Title = "DreamDaemon",
					Icon = "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg"
				};
				SetDDRunning(true); //vOv
			}

			var newChildren = new List<ITreeNode>
			{
				instanceUserTreeNode,
				new InstanceUserRootViewModel(pageContext, instanceClient.Users, instanceUserTreeNode, userProvider, this),
				new RepositoryViewModel(pageContext, instanceClient.Repository, instanceClient.DreamMaker, instanceJobSink, instanceUserTreeNode),
				instanceUser.ByondRights != ByondRights.None ? new BasicNode
				{
					Title = "TODO: Byond",
					Icon = "resm:Tgstation.Server.ControlPanel.Assets.byond.jpg"
				} : new BasicNode
				{
					Title = "Byond",
					Icon = "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg"
				},
				instanceUser.DreamMakerRights != DreamMakerRights.None ? new BasicNode
				{
					Title = "TODO: Compiler",
					Icon = "resm:Tgstation.Server.ControlPanel.Assets.dreammaker.ico"
				} : new BasicNode
				{
					Title = "Compiler",
					Icon = "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg"
				},
				ddNode,
				instanceUser.ChatBotRights != ChatBotRights.None ? new BasicNode
				{
					Title = "TODO: Chat Bots",
					Icon = "resm:Tgstation.Server.ControlPanel.Assets.chat.png"
				} : new BasicNode
				{
					Title = "Chat Bots",
					Icon = "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg"
				},
				instanceUser.ConfigurationRights != ConfigurationRights.None ? new BasicNode
				{
					Title = "TODO: Static Files",
					Icon = "resm:Tgstation.Server.ControlPanel.Assets.folder.png"
				} : new BasicNode
				{
					Title = "Static Files",
					Icon = "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg"
				},
			};

			using (DelayChangeNotifications())
			{
				Children = newChildren;
				this.RaisePropertyChanged(nameof(Icon));
			}

			if (canReadDD)
			{
				var dd = await instanceClient.DreamDaemon.Read(cancellationToken).ConfigureAwait(false);
				using (DelayChangeNotifications())
				{
					SetDDRunning(dd.Running.Value);
					var ddIndex = newChildren.IndexOf(ddNode);
					ddNode = new BasicNode
					{
						Title = "TODO: DreamDaemon",
						Icon = dd.Running.Value ? "resm:Tgstation.Server.ControlPanel.Assets.dd.ico" : "resm:Tgstation.Server.ControlPanel.Assets.dd_down.ico"
					};
					newChildren = new List<ITreeNode>(newChildren)
					{
						[ddIndex] = ddNode
					};
					Children = newChildren;
				}
			}
		}

		public Task HandleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(InstanceCommand command)
		{
			switch (command)
			{
				case InstanceCommand.Close:
					return true;
				case InstanceCommand.Save:
				case InstanceCommand.FixPerms:
					return !loading;
				case InstanceCommand.Delete:
					return userRightsProvider.InstanceManagerRights.HasFlag(InstanceManagerRights.Delete);
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
		public async Task Refresh(CancellationToken cancellationToken)
		{
			loading = true;
			try
			{
				Save.Recheck();
				Delete.Recheck();
				FixPerms.Recheck();
				Instance = await instanceManagerClient.GetId(Instance, cancellationToken).ConfigureAwait(true);
				await PostRefresh(cancellationToken).ConfigureAwait(true);
			}
			finally
			{
				loading = false;
				Save.Recheck();
				Delete.Recheck();
				FixPerms.Recheck();
			}
		}

		public async Task RunCommand(InstanceCommand command, CancellationToken cancellationToken)
		{
			async Task Update(Instance newInstance)
			{
				loading = true;
				try
				{
					Save.Recheck();
					Delete.Recheck();
					FixPerms.Recheck();
					Instance = await instanceManagerClient.Update(newInstance, cancellationToken).ConfigureAwait(true);
					await PostRefresh(cancellationToken).ConfigureAwait(true);
				}
				finally
				{
					loading = false;
					Save.Recheck();
					Delete.Recheck();
					FixPerms.Recheck();
				}
			}

			try
			{
				switch (command)
				{
					case InstanceCommand.Close:
						pageContext.ActiveObject = null;
						break;
					case InstanceCommand.Save:
						var newInstance = new Instance
						{
							Id = Instance.Id
						};
						if (!String.IsNullOrWhiteSpace(NewName) && userRightsProvider.InstanceManagerRights.HasFlag(InstanceManagerRights.Rename))
							newInstance.Name = NewName;
						if (!String.IsNullOrWhiteSpace(NewPath) && userRightsProvider.InstanceManagerRights.HasFlag(InstanceManagerRights.Relocate))
							newInstance.Path = NewPath;
						if (userRightsProvider.InstanceManagerRights.HasFlag(InstanceManagerRights.SetOnline))
							newInstance.Online = Enabled;
						if (userRightsProvider.InstanceManagerRights.HasFlag(InstanceManagerRights.SetConfiguration))
							newInstance.ConfigurationType = ConfigMode;
						if (userRightsProvider.InstanceManagerRights.HasFlag(InstanceManagerRights.SetAutoUpdate))
							newInstance.AutoUpdateInterval = AutoUpdateInterval;

						await Update(newInstance).ConfigureAwait(true);
						break;
					case InstanceCommand.Delete:
						if (!deleteConfirming)
						{
							async void ResetDelete()
							{
								await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(true);
								deleteConfirming = false;
								this.RaisePropertyChanged(nameof(DeleteText));
							}
							deleteConfirming = true;
							this.RaisePropertyChanged(nameof(DeleteText));
							ResetDelete();
							break;
						}
						pageContext.ActiveObject = null;
						await instanceManagerClient.Detach(instance, cancellationToken).ConfigureAwait(true);
						await instanceRootViewModel.Refresh(cancellationToken).ConfigureAwait(true);
						break;
					case InstanceCommand.FixPerms:
						await Update(new Instance
						{
							Id = Instance.Id
						}).ConfigureAwait(true);
						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
				}
			}
			catch (InsufficientPermissionsException)
			{
				pageContext.ActiveObject = null;
				await instanceRootViewModel.Refresh(cancellationToken).ConfigureAwait(false);
			}
		}
	}
}