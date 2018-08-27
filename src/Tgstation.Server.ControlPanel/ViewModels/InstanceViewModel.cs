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
			Refresh,
			Save,
			Delete
		}

		public string Title => Instance.Name;

		public bool IsExpanded { get; set; }

		public string Icon => instance.Online.Value ? (ddRunning ? "resm:Tgstation.Server.ControlPanel.Assets.database_on.jpg" : "resm:Tgstation.Server.ControlPanel.Assets.database.png") : "resm:Tgstation.Server.ControlPanel.Assets.database_down.png";


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
		public ConfigurationType ConfigType
		{
			get => configType;
			set => this.RaiseAndSetIfChanged(ref configType, value);
		}
		public int AutoUpdateInterval
		{
			get => autoUpdateInterval;
			set => this.RaiseAndSetIfChanged(ref autoUpdateInterval, value);
		}

		public EnumCommand<InstanceCommand> Close { get; }
		public EnumCommand<InstanceCommand> Refresh { get; }
		public EnumCommand<InstanceCommand> Save { get; }
		public EnumCommand<InstanceCommand> Delete { get; }

		readonly IInstanceManagerClient instanceManagerClient;
		readonly IInstanceClient instanceClient;
		readonly PageContextViewModel pageContext;
		readonly IUserRightsProvider userRightsProvider;

		IReadOnlyList<ITreeNode> children;
		Instance instance;
		bool ddRunning;

		bool enabled;
		string newName;
		string newPath;
		ConfigurationType configType;
		int autoUpdateInterval;

		bool deleteConfirming;

		public InstanceViewModel(IInstanceManagerClient instanceManagerClient, PageContextViewModel pageContext, Instance instance, IUserRightsProvider userRightsProvider)
		{
			this.instanceManagerClient = instanceManagerClient ?? throw new ArgumentNullException(nameof(instanceManagerClient));
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			Instance = instance ?? throw new ArgumentNullException(nameof(instance));
			this.userRightsProvider = userRightsProvider ?? throw new ArgumentNullException(nameof(userRightsProvider));

			instanceClient = instanceManagerClient.CreateClient(instance);

			PostRefresh();

			Close = new EnumCommand<InstanceCommand>(InstanceCommand.Close, this);
			Refresh = new EnumCommand<InstanceCommand>(InstanceCommand.Refresh, this);
			Save = new EnumCommand<InstanceCommand>(InstanceCommand.Save, this);
			Delete = new EnumCommand<InstanceCommand>(InstanceCommand.Delete, this);

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
				}
			};
		}

		public void SetDDRunning(bool yes)
		{
			ddRunning = yes;
			this.RaisePropertyChanged(nameof(Icon));
		}
		
		void PostRefresh()
		{
			Enabled = Instance.Online.Value;
			ConfigType = Instance.ConfigurationType.Value;
			AutoUpdateInterval = Instance.AutoUpdateInterval.Value;
		}

		public Task HandleDoubleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(InstanceCommand command)
		{
			switch (command)
			{
				case InstanceCommand.Close:
				case InstanceCommand.Refresh:
					return true;
				case InstanceCommand.Save:
					return false;
				case InstanceCommand.Delete:
					return userRightsProvider.InstanceManagerRights.HasFlag(InstanceManagerRights.Delete);
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public Task RunCommand(InstanceCommand command, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}
	}
}