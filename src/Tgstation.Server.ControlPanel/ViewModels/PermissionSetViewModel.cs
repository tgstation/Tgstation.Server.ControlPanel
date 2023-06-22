using System;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Models.Request;
using Tgstation.Server.Api.Models.Response;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class PermissionSetViewModel : ViewModelBase, ICommandReceiver<PermissionSetViewModel.PermissionSetCommand>
	{
		public enum PermissionSetCommand
		{
			Save,
		}

		public EnumCommand<PermissionSetCommand> Save { get; }

		public bool InstanceCreate
		{
			get => newInstanceManagerRights.HasFlag(InstanceManagerRights.Create);
			set
			{
				var right = InstanceManagerRights.Create;
				if (value)
					newInstanceManagerRights |= right;
				else
					newInstanceManagerRights &= ~right;
			}
		}
		public bool InstanceRead
		{
			get => newInstanceManagerRights.HasFlag(InstanceManagerRights.Read);
			set
			{
				var right = InstanceManagerRights.Read;
				if (value)
					newInstanceManagerRights |= right;
				else
					newInstanceManagerRights &= ~right;
			}
		}
		public bool InstanceRename
		{
			get => newInstanceManagerRights.HasFlag(InstanceManagerRights.Rename);
			set
			{
				var right = InstanceManagerRights.Rename;
				if (value)
					newInstanceManagerRights |= right;
				else
					newInstanceManagerRights &= ~right;
			}
		}
		public bool InstanceRelocate
		{
			get => newInstanceManagerRights.HasFlag(InstanceManagerRights.Relocate);
			set
			{
				var right = InstanceManagerRights.Relocate;
				if (value)
					newInstanceManagerRights |= right;
				else
					newInstanceManagerRights &= ~right;
			}
		}
		public bool InstanceOnline
		{
			get => newInstanceManagerRights.HasFlag(InstanceManagerRights.SetOnline);
			set
			{
				var right = InstanceManagerRights.SetOnline;
				if (value)
					newInstanceManagerRights |= right;
				else
					newInstanceManagerRights &= ~right;
			}
		}
		public bool InstanceDelete
		{
			get => newInstanceManagerRights.HasFlag(InstanceManagerRights.Delete);
			set
			{
				var right = InstanceManagerRights.Delete;
				if (value)
					newInstanceManagerRights |= right;
				else
					newInstanceManagerRights &= ~right;
			}
		}
		public bool InstanceList
		{
			get => newInstanceManagerRights.HasFlag(InstanceManagerRights.List);
			set
			{
				var right = InstanceManagerRights.List;
				if (value)
					newInstanceManagerRights |= right;
				else
					newInstanceManagerRights &= ~right;
			}
		}
		public bool InstanceConfig
		{
			get => newInstanceManagerRights.HasFlag(InstanceManagerRights.SetConfiguration);
			set
			{
				var right = InstanceManagerRights.SetConfiguration;
				if (value)
					newInstanceManagerRights |= right;
				else
					newInstanceManagerRights &= ~right;
			}
		}
		public bool InstanceUpdate
		{
			get => newInstanceManagerRights.HasFlag(InstanceManagerRights.SetAutoUpdate);
			set
			{
				var right = InstanceManagerRights.SetAutoUpdate;
				if (value)
					newInstanceManagerRights |= right;
				else
					newInstanceManagerRights &= ~right;
			}
		}
		public bool InstanceChatLimit
		{
			get => newInstanceManagerRights.HasFlag(InstanceManagerRights.SetChatBotLimit);
			set
			{
				var right = InstanceManagerRights.SetChatBotLimit;
				if (value)
					newInstanceManagerRights |= right;
				else
					newInstanceManagerRights &= ~right;
			}
		}
		public bool InstanceGrant
		{
			get => newInstanceManagerRights.HasFlag(InstanceManagerRights.GrantPermissions);
			set
			{
				var right = InstanceManagerRights.GrantPermissions;
				if (value)
					newInstanceManagerRights |= right;
				else
					newInstanceManagerRights &= ~right;
			}
		}
		public bool AdminWriteUsers
		{
			get => newAdministrationRights.HasFlag(AdministrationRights.WriteUsers);
			set
			{
				var right = AdministrationRights.WriteUsers;
				if (value)
					newAdministrationRights |= right;
				else
					newAdministrationRights &= ~right;
			}
		}
		public bool AdminReadUsers
		{
			get => newAdministrationRights.HasFlag(AdministrationRights.ReadUsers);
			set
			{
				var right = AdministrationRights.ReadUsers;
				if (value)
					newAdministrationRights |= right;
				else
					newAdministrationRights &= ~right;
			}
		}
		public bool AdminEditPassword
		{
			get => newAdministrationRights.HasFlag(AdministrationRights.EditOwnPassword);
			set
			{
				var right = AdministrationRights.EditOwnPassword;
				if (value)
					newAdministrationRights |= right;
				else
					newAdministrationRights &= ~right;
			}
		}

		public bool AdminRestartServer
		{
			get => newAdministrationRights.HasFlag(AdministrationRights.RestartHost);
			set
			{
				var right = AdministrationRights.RestartHost;
				if (value)
					newAdministrationRights |= right;
				else
					newAdministrationRights &= ~right;
			}
		}

		public bool AdminChangeVersion
		{
			get => newAdministrationRights.HasFlag(AdministrationRights.ChangeVersion);
			set
			{
				var right = AdministrationRights.ChangeVersion;
				if (value)
					newAdministrationRights |= right;
				else
					newAdministrationRights &= ~right;
			}
		}
		public bool AdminLogs
		{
			get => newAdministrationRights.HasFlag(AdministrationRights.DownloadLogs);
			set
			{
				var right = AdministrationRights.DownloadLogs;
				if (value)
					newAdministrationRights |= right;
				else
					newAdministrationRights &= ~right;
			}
		}

		public bool AdminUpload
		{
			get => newAdministrationRights.HasFlag(AdministrationRights.UploadVersion);
			set
			{
				var right = AdministrationRights.UploadVersion;
				if (value)
					newAdministrationRights |= right;
				else
					newAdministrationRights &= ~right;
			}
		}

		public PermissionSet PermissionSet => user?.PermissionSet ?? group.PermissionSet;
		bool CanEditRights => userRightsProvider.AdministrationRights.HasFlag(AdministrationRights.WriteUsers);

		readonly IUserRightsProvider userRightsProvider;
		readonly IUsersClient usersClient;
		readonly IUserGroupsClient groupsClient;

		UserResponse user;
		UserGroupResponse group;

		InstanceManagerRights newInstanceManagerRights;
		AdministrationRights newAdministrationRights;

		public PermissionSetViewModel(IUserRightsProvider userRightsProvider, IUsersClient usersClient, UserResponse user)
		{
			this.userRightsProvider = userRightsProvider ?? throw new ArgumentNullException(nameof(userRightsProvider));
			this.usersClient = usersClient ?? throw new ArgumentNullException(nameof(usersClient));
			this.user = user ?? throw new ArgumentNullException(nameof(user));

			newAdministrationRights = PermissionSet.AdministrationRights.Value;
			newInstanceManagerRights = PermissionSet.InstanceManagerRights.Value;

			Save = new EnumCommand<PermissionSetCommand>(PermissionSetCommand.Save, this);
		}

		public PermissionSetViewModel(IUserRightsProvider userRightsProvider, IUserGroupsClient groupsClient, UserGroupResponse group)
		{
			this.userRightsProvider = userRightsProvider ?? throw new ArgumentNullException(nameof(userRightsProvider));
			this.groupsClient = groupsClient ?? throw new ArgumentNullException(nameof(groupsClient));
			this.group = group ?? throw new ArgumentNullException(nameof(group));

			newAdministrationRights = PermissionSet.AdministrationRights.Value;
			newInstanceManagerRights = PermissionSet.InstanceManagerRights.Value;

			Save = new EnumCommand<PermissionSetCommand>(PermissionSetCommand.Save, this);
		}

		async Task SaveImpl(CancellationToken cancellationToken)
		{
			var permissionSet = new PermissionSet
			{
				AdministrationRights = newAdministrationRights,
				InstanceManagerRights = newInstanceManagerRights,
			};

			if (user != null)
			{
				user = await usersClient.Update(
					new UserUpdateRequest
					{
						Id = user.Id,
						PermissionSet = permissionSet,
					},
					cancellationToken);
				return;
			}

			group = await groupsClient.Update(
				new UserGroupUpdateRequest
				{
					Id = group.Id,
					PermissionSet = permissionSet,
				},
				cancellationToken);
		}

		public bool CanRunCommand(PermissionSetCommand command) => CanEditRights;

		public Task RunCommand(PermissionSetCommand command, CancellationToken cancellationToken)
		{
			return SaveImpl(cancellationToken);
		}
	}
}
