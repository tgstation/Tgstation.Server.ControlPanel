using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Models.Response;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class UsersRootViewModel : ViewModelBase, ITreeNode, IUserProvider
	{
		public string Title => "Users";

		public string Icon
		{
			get => icon;
			set => this.RaiseAndSetIfChanged(ref icon, value);
		}
		public bool IsExpanded { get; set; }

		public UserResponse CurrentUser => currentUser.User;

		public IReadOnlyList<ITreeNode> Children
		{
			get => children;
			set => this.RaiseAndSetIfChanged(ref children, value);
		}

		readonly IUsersClient usersClient;
		readonly PageContextViewModel pageContext;
		readonly UserViewModel currentUser;
		readonly ServerInformationResponse serverInformation;

		readonly UserGroupRootViewModel groupsRootViewModel;
		IReadOnlyList<ITreeNode> children;
		IReadOnlyList<UserResponse> lastUsers;
		bool loading;

		string icon;

		public UsersRootViewModel(IUsersClient usersClient, IUserGroupsClient groupsClient, ServerInformationResponse serverInformation, PageContextViewModel pageContext, UserViewModel currentUser)
		{
			this.usersClient = usersClient ?? throw new ArgumentNullException(nameof(usersClient));
			this.serverInformation = serverInformation ?? throw new ArgumentNullException(nameof(serverInformation));
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));


			groupsRootViewModel = new UserGroupRootViewModel(usersClient, this, pageContext, serverInformation, currentUser, groupsClient);
			async void FirstLoad() => await Refresh(default).ConfigureAwait(false);
			FirstLoad();
			currentUser.OnUpdated += (a, b) => FirstLoad();
		}

		public async Task Refresh(CancellationToken cancellationToken)
		{
			lock (this)
			{
				if (loading)
					return;
				loading = true;
			}

			if (!currentUser.AdministrationRights.HasFlag(AdministrationRights.ReadUsers))
			{
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg";
				Children = null;
				loading = false;
				return;
			}

			var auvm = new AddUserViewModel(pageContext, serverInformation, usersClient, this);
			var basic = new BasicNode()
			{
				Title = "Loading...",
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png"
			};
			Children = new List<ITreeNode>
			{
				groupsRootViewModel,
				basic
			};

			try
			{
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.folder.png";
				var groupsRefreshTask = groupsRootViewModel.Refresh(cancellationToken);
				lastUsers = await usersClient.List(null, cancellationToken).ConfigureAwait(false);
				await groupsRefreshTask.ConfigureAwait(false);
				var newChildren = new List<ITreeNode>
				{
					groupsRootViewModel
				};

				if (currentUser.AdministrationRights.HasFlag(AdministrationRights.WriteUsers))
					newChildren.Add(lastUsers.Count < serverInformation.UserLimit ? (ITreeNode)auvm : new BasicNode
					{
						Title = "User Limit Reached",
						Icon = "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg"
					});
				newChildren.AddRange(lastUsers.Where(x => x.Id != currentUser.User.Id).Select(x => new UserViewModel(usersClient, serverInformation, x, pageContext, currentUser)));
				Children = newChildren;
			}
			catch (InsufficientPermissionsException)
			{
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg";
				Children = null;
			}
			catch
			{
				basic.Title = "Error!";
				basic.Icon = "resm:Tgstation.Server.ControlPanel.Assets.error.png";
			}
			finally
			{
				loading = false;
			}
		}

		public void DirectAdd(UserResponse user)
		{
			var newModel = new UserViewModel(usersClient, serverInformation, user, pageContext, currentUser);
			var newChildren = new List<ITreeNode>(Children)
			{
				newModel
			};
			lastUsers = new List<UserResponse>(lastUsers)
			{
				user
			};
			Children = newChildren;
			pageContext.ActiveObject = newModel;
		}

		public Task HandleClick(CancellationToken cancellationToken) => Refresh(cancellationToken);

		public IReadOnlyList<UserResponse> GetUsers() => lastUsers;

		public IReadOnlyList<UserGroupResponse> GetGroups() => groupsRootViewModel?.GetGroups();

		public void ForceUpdate(UserResponse updatedUser)
		{
			if (updatedUser.Id == currentUser.User.Id)
			{
				currentUser.User = updatedUser;
				return;
			}

			var updatedLastUsers = new List<UserResponse>(lastUsers);
			updatedLastUsers.RemoveAll(x => x.Id == updatedUser.Id);
			updatedLastUsers.Add(updatedUser);

			var newModel = new UserViewModel(usersClient, serverInformation, updatedUser, pageContext, currentUser);
			var newChildren = new List<ITreeNode>(Children);

			newChildren.RemoveAll(x => x is UserViewModel uvm && uvm.User.Id == updatedUser.Id);
			newChildren.Add(newModel);

			lastUsers = updatedLastUsers;
			Children = newChildren;
		}
	}
}
