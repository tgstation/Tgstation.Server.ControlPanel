using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class UsersRootViewModel : ViewModelBase, ITreeNode
	{
		public string Title => "Users";

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
		readonly PageContextViewModel pageContext;
		readonly UserViewModel currentUser;

		IReadOnlyList<ITreeNode> children;
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

			if (!currentUser.AdministrationRights.HasFlag(AdministrationRights.EditUsers))
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
				var users = await usersClient.List(cancellationToken).ConfigureAwait(false);
				var newChildren = new List<ITreeNode>
				{
					auvm
				};
				newChildren.AddRange(users.Where(x => x.Id != currentUser.User.Id).Select(x => new UserViewModel(usersClient, x, pageContext, currentUser)));
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

		public Task HandleDoubleClick(CancellationToken cancellationToken) => Refresh(cancellationToken);
	}
}
