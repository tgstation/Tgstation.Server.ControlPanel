using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class UserGroupRootViewModel : ViewModelBase, ITreeNode, IGroupsProvider
	{
		public string Title => "Groups";

		public string Icon
		{
			get => icon;
			set => this.RaiseAndSetIfChanged(ref icon, value);
		}

		public bool IsExpanded { get; set; }

		public IReadOnlyList<ITreeNode> Children
		{
			get => children;
			set => this.RaiseAndSetIfChanged(ref children, value);
		}

		readonly IUsersClient usersClient;
		readonly IUserProvider userProvider;
		readonly PageContextViewModel pageContext;
		readonly IUserGroupsClient groupsClient;
		readonly ServerInformation serverInformation;
		readonly IUserRightsProvider rightsProvider;

		IReadOnlyList<UserGroup> lastGroups;
		IReadOnlyList<ITreeNode> children;

		bool loading;
		string icon;

		public UserGroupRootViewModel(
			IUsersClient usersClient,
			IUserProvider userProvider,
			PageContextViewModel pageContext,
			ServerInformation serverInformation,
			IUserRightsProvider rightsProvider,
			IUserGroupsClient userGroupsClient)
		{
			this.usersClient = usersClient ?? throw new ArgumentNullException(nameof(usersClient));
			this.userProvider = userProvider ?? throw new ArgumentNullException(nameof(userProvider));
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.groupsClient = userGroupsClient ?? throw new ArgumentNullException(nameof(userGroupsClient));
			this.serverInformation = serverInformation ?? throw new ArgumentNullException(nameof(serverInformation));
			this.rightsProvider = rightsProvider ?? throw new ArgumentNullException(nameof(rightsProvider));
			async void FirstLoad() => await Refresh(default).ConfigureAwait(false);
			FirstLoad();
			rightsProvider.OnUpdated += (a, b) => FirstLoad();
		}

		public async Task Refresh(CancellationToken cancellationToken)
		{
			lock (this)
			{
				if (loading)
					return;
				loading = true;
			}

			var auvm = new AddGroupViewModel(pageContext, serverInformation, groupsClient, rightsProvider, this);
			var basic = new BasicNode()
			{
				Title = "Loading...",
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png"
			};
			Children = new List<ITreeNode>
			{
				basic
			};

			try
			{
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.folder.png";
				lastGroups = await groupsClient.List(null, cancellationToken).ConfigureAwait(false);
				var newChildren = new List<ITreeNode>();
				if (rightsProvider.AdministrationRights.HasFlag(AdministrationRights.WriteUsers))
					newChildren.Add(lastGroups.Count < serverInformation.UserGroupLimit ? (ITreeNode)auvm : new BasicNode
					{
						Title = "Group Limit Reached",
						Icon = "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg"
					});
				newChildren.AddRange(lastGroups.Select(x => new UserGroupViewModel(userProvider, groupsClient, usersClient, x, pageContext, rightsProvider)));
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
		public Task HandleClick(CancellationToken cancellationToken) => Refresh(cancellationToken);

		public IReadOnlyList<UserGroup> GetGroups() => lastGroups?.ToList();

		public void DirectAdd(UserGroup group)
		{
			var lastGroupsList = lastGroups.ToList();
			lastGroupsList.Add(group);
			lastGroups = lastGroupsList;

			var lastChildrenList = Children.ToList();
			lastChildrenList.Add(new UserGroupViewModel(userProvider, groupsClient, usersClient, group, pageContext, rightsProvider));
			Children = lastChildrenList;
		}
	}
}