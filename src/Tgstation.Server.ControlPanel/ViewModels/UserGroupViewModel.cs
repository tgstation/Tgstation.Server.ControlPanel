using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class UserGroupViewModel : ViewModelBase, ITreeNode, ICommandReceiver<UserGroupViewModel.UserGroupsCommand>
	{
		public enum UserGroupsCommand
		{
			Close,
			Delete,
			AddUser,
			Refresh,
		}

		public string Title => group.Name;

		public string Icon => "resm:Tgstation.Server.ControlPanel.Assets.group.png";

		public bool IsExpanded { get; set; }

		public long Id => group.Id;

		public long UserCount => group.Users.Count;

		public bool Loading
		{
			get => loading;
			set
			{
				this.RaiseAndSetIfChanged(ref loading, value);
				Refresh.Recheck();
				Delete.Recheck();
				AddUser.Recheck();
			}
		}

		public string Error
		{
			get => error;
			set
			{
				this.RaiseAndSetIfChanged(ref error, value);
				this.RaisePropertyChanged(nameof(HasError));
			}
		}

		public bool HasError => !String.IsNullOrWhiteSpace(Error);

		public PermissionSetViewModel PermissionSetViewModel { get; private set; }

		public IReadOnlyList<ITreeNode> Children => null;
		public EnumCommand<UserGroupsCommand> Delete { get; }
		public EnumCommand<UserGroupsCommand> Close { get; }
		public EnumCommand<UserGroupsCommand> Refresh { get; }
		public EnumCommand<UserGroupsCommand> AddUser { get; }
		public UserGroup Group
		{
			get => group;
			set
			{
				this.RaiseAndSetIfChanged(ref group, value);
			}
		}

		public bool CanAdd => UserStrings.Count > 0;

		public IReadOnlyList<string> UserStrings { get; set; }

		public int SelectedIndex { get; set; }

		readonly IUserProvider userProvider;
		readonly IUserGroupsClient groupsClient;
		readonly IUsersClient usersClient;
		readonly PageContextViewModel pageContext;
		readonly IUserRightsProvider rightsProvider;

		UserGroup group;
		bool loading;
		string error;

		public UserGroupViewModel(IUserProvider userProvider, IUserGroupsClient groupsClient, IUsersClient usersClient, UserGroup group, PageContextViewModel pageContext, IUserRightsProvider rightsProvider)
		{
			this.userProvider = userProvider ?? throw new ArgumentNullException(nameof(userProvider));
			this.groupsClient = groupsClient ?? throw new ArgumentNullException(nameof(groupsClient));
			this.usersClient = usersClient ?? throw new ArgumentNullException(nameof(usersClient));
			Group = group ?? throw new ArgumentNullException(nameof(group));
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.rightsProvider = rightsProvider ?? throw new ArgumentNullException(nameof(rightsProvider));

			Close = new EnumCommand<UserGroupsCommand>(UserGroupsCommand.Close, this);
			Refresh = new EnumCommand<UserGroupsCommand>(UserGroupsCommand.Refresh, this);
			AddUser = new EnumCommand<UserGroupsCommand>(UserGroupsCommand.AddUser, this);
			Delete = new EnumCommand<UserGroupsCommand>(UserGroupsCommand.Delete, this);

			UpdateUserStrings();
		}

		void UpdateUserStrings()
		{
			using (DelayChangeNotifications())
			{
				UserStrings = GetFilteredUsers().Select(x => $"{x.Id}: {x.Name}").ToList();
				SelectedIndex = 0;
				PermissionSetViewModel = new PermissionSetViewModel(rightsProvider, groupsClient, Group);
				this.RaisePropertyChanged(nameof(UserStrings));
				this.RaisePropertyChanged(nameof(SelectedIndex));
				this.RaisePropertyChanged(nameof(PermissionSetViewModel));
				this.RaisePropertyChanged(nameof(CanAdd));
				AddUser.Recheck();
			}
		}

		async Task RefreshGroup(bool force, CancellationToken cancellationToken)
		{
			if (!force)
				lock (this)
				{
					if (Loading)
						return;
					Loading = true;
				}

			try
			{
				Error = null;
				Group = await groupsClient.GetId(Group, cancellationToken).ConfigureAwait(false);
				UpdateUserStrings();
			}
			catch(Exception ex)
			{
				Error = ex.Message;
			}
			finally
			{
				Loading = false;
			}
		}

		public Task HandleClick(CancellationToken cancellationToken)
		{
			this.RaisePropertyChanged(nameof(CanAdd));
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(UserGroupsCommand command)
		{
			switch (command)
			{
				case UserGroupsCommand.AddUser:
					return !Loading && SelectedIndex < UserStrings.Count && userProvider.GetUsers() != null && rightsProvider.AdministrationRights.HasFlag(AdministrationRights.WriteUsers);
				case UserGroupsCommand.Close:
					return true;
				case UserGroupsCommand.Delete:
					return !Loading && rightsProvider.AdministrationRights.HasFlag(AdministrationRights.WriteUsers) && UserCount == 0;
				case UserGroupsCommand.Refresh:
					return !Loading && rightsProvider.AdministrationRights.HasFlag(AdministrationRights.ReadUsers);
			}

			return false;
		}

		List<User> GetFilteredUsers()
		{
			return userProvider
				.GetUsers()
				?.Where(x => !Group.Users.Any(y => y.Id == x.Id))
				.ToList()
				?? new List<User>();
		}

		public async Task RunCommand(UserGroupsCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case UserGroupsCommand.Close:
					pageContext.ActiveObject = null;
					break;
				case UserGroupsCommand.Delete:
					await groupsClient.Delete(group, cancellationToken).ConfigureAwait(false);
					pageContext.ActiveObject = null;
					break;
				case UserGroupsCommand.Refresh:
					await RefreshGroup(false, cancellationToken).ConfigureAwait(false);
					break;
				case UserGroupsCommand.AddUser:
					await DoAddUser(cancellationToken).ConfigureAwait(false);
					break;
			}
		}

		async Task DoAddUser(CancellationToken cancellationToken)
		{
			lock (this)
			{
				if (Loading)
					return;
				Loading = true;
			}

			try
			{
				var userToAdd = GetFilteredUsers()[SelectedIndex];

				var updatedUser = await usersClient.Update(new UserUpdate
				{
					Id = userToAdd.Id,
					Group = new Api.Models.Internal.UserGroup
					{
						Id = Group.Id
					}
				},
				cancellationToken);
				userProvider.ForceUpdate(updatedUser);
				await RefreshGroup(true, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Error = ex.Message;
			}
			finally
			{
				Loading = false;
			}
		}
	}
}