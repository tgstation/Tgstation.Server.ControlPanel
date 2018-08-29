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
	sealed class UsersRootViewModel : ViewModelBase, ITreeNode, IUserProvider
	{
		public string Title => "Users";

		public string Icon
		{
			get => icon;
			set => this.RaiseAndSetIfChanged(ref icon, value);
		}
		public bool IsExpanded { get; set; }

		public User CurrentUser => currentUser.User;

		public IReadOnlyList<ITreeNode> Children
		{
			get => children;
			set => this.RaiseAndSetIfChanged(ref children, value);
		}

		readonly IUsersClient usersClient;
		readonly PageContextViewModel pageContext;
		readonly UserViewModel currentUser;

		IReadOnlyList<ITreeNode> children;
		IReadOnlyList<User> lastUsers;
		bool loading;

		string icon;

		public UsersRootViewModel(IUsersClient usersClient, PageContextViewModel pageContext, UserViewModel currentUser)
		{
			this.usersClient = usersClient ?? throw new ArgumentNullException(nameof(usersClient));
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));


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

			if (!currentUser.AdministrationRights.HasFlag(AdministrationRights.WriteUsers))
			{
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg";
				Children = null;
				loading = false;
				return;
			}

			var auvm = new AddUserViewModel(pageContext, usersClient, this);
			var basic = new BasicNode()
			{
				Title = "Loading...",
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png"
			};
			Children = new List<ITreeNode>
			{
				auvm,
				basic
			};

			try
			{
				lastUsers = await usersClient.List(cancellationToken).ConfigureAwait(false);
				var newChildren = new List<ITreeNode>
				{
					auvm
				};
				newChildren.AddRange(lastUsers.Where(x => x.Id != currentUser.User.Id).Select(x => new UserViewModel(usersClient, x, pageContext, currentUser)));
				Children = newChildren;
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.folder.png";
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

		public void DirectAdd(User user)
		{
			var newModel = new UserViewModel(usersClient, user, pageContext, currentUser);
			var newChildren = new List<ITreeNode>(Children)
			{
				newModel
			};
			lastUsers = new List<User>(lastUsers)
			{
				user
			};
			Children = newChildren;
			pageContext.ActiveObject = newModel;
		}

		public Task HandleDoubleClick(CancellationToken cancellationToken) => Refresh(cancellationToken);

		public IReadOnlyList<User> GetUsers() => lastUsers;
	}
}
