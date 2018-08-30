using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class InstanceUserRootViewModel : ViewModelBase, ITreeNode, IUserProvider
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

		public User CurrentUser => userProvider.CurrentUser;

		readonly PageContextViewModel pageContext;
		readonly IInstanceUserClient instanceUserClient;
		readonly IInstanceUserRightsProvider rightsProvider;
		readonly IUserProvider userProvider;
		readonly InstanceViewModel instanceViewModel;

		IReadOnlyList<ITreeNode> children;
		IReadOnlyList<InstanceUser> activeUsers;

		bool loading;
		string icon;


		public InstanceUserRootViewModel(PageContextViewModel pageContext, IInstanceUserClient instanceUserClient, IInstanceUserRightsProvider rightsProvider, IUserProvider userProvider, InstanceViewModel instanceViewModel)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.instanceUserClient = instanceUserClient ?? throw new ArgumentNullException(nameof(instanceUserClient));
			this.rightsProvider = rightsProvider ?? throw new ArgumentNullException(nameof(rightsProvider));
			this.userProvider = userProvider ?? throw new ArgumentNullException(nameof(userProvider));
			this.instanceViewModel = instanceViewModel ?? throw new ArgumentNullException(nameof(instanceViewModel));

			Icon = "resm:Tgstation.Server.ControlPanel.Assets.folder.png";

			async void InitialLoad() => await Refresh(default).ConfigureAwait(false);
			InitialLoad();
		}

		public static string GetDisplayNameForInstanceUser(IUserProvider userProvider, InstanceUser user) => userProvider.GetUsers()?
						.Where(x => x.Id == user.UserId).Select(x => String.Format(CultureInfo.InvariantCulture, "{0} ({1})", x.Name, x.Id)).FirstOrDefault()
						?? String.Format(CultureInfo.InvariantCulture, "User ID: {0}", user.UserId);

		public async Task Refresh(CancellationToken cancellationToken)
		{
			lock (this)
			{
				if (loading)
					return;
				loading = true;
			}
			var nullPage = true;
			try
			{
				if(rightsProvider.InstanceUserRights == InstanceUserRights.None)
				{
					Children = null;
					Icon = "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg";
					return;
				}

				Children = null;
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png";
				this.RaisePropertyChanged(nameof(Icon));

				var newChildren = new List<ITreeNode>();

				var hasReadRight = rightsProvider.InstanceUserRights.HasFlag(InstanceUserRights.ReadUsers);

				if (hasReadRight)
					activeUsers = (await instanceUserClient.List(cancellationToken).ConfigureAwait(true)).Where(x => x.UserId != userProvider.CurrentUser.Id).ToList();

				if (rightsProvider.InstanceUserRights.HasFlag(InstanceUserRights.WriteUsers))
					newChildren.Add(new AddInstanceUserViewModel(pageContext, this, instanceUserClient, rightsProvider, this));

				if(hasReadRight)
					newChildren.AddRange(activeUsers
						.Select(x => new InstanceUserViewModel(pageContext, instanceViewModel, rightsProvider, instanceUserClient, x,
						GetDisplayNameForInstanceUser(userProvider, x),
						rightsProvider, this)));

				Children = newChildren;
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.folder.png";
				nullPage = false;
			}
			catch
			{
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.error.png";
			}
			finally
			{
				loading = false;
				if (pageContext.IsAddInstanceUser || pageContext.IsInstanceUser)
					if (nullPage)
						pageContext.ActiveObject = false;
					else if (pageContext.IsAddInstanceUser)
						if (Children[0] is AddInstanceUserViewModel)
							pageContext.ActiveObject = Children[0];
						else
							pageContext.ActiveObject = null;
					else //instance user
					{
						var start = Children[0] is AddInstanceUserViewModel ? 1 : 0;
						bool found = false;
						for (var I = start; I < Children.Count; ++I)
							if (((InstanceUserViewModel)Children[I]).Id == ((InstanceUserViewModel)pageContext.ActiveObject).Id)
							{
								pageContext.ActiveObject = Children[I];
								found = true;
							}
						if (!found)
							pageContext.ActiveObject = null;
					}
			}
		}

		public void DirectAdd(InstanceUser user)
		{
			var newModel = new InstanceUserViewModel(pageContext, instanceViewModel, rightsProvider, instanceUserClient, user, GetDisplayNameForInstanceUser(userProvider, user), rightsProvider, this);
			var newChildren = new List<ITreeNode>(Children)
			{
				newModel
			};
			activeUsers = new List<InstanceUser>(activeUsers)
			{
				user
			};
			if (newChildren[0] is AddInstanceUserViewModel)
				newChildren[0] = new AddInstanceUserViewModel(pageContext, this, instanceUserClient, rightsProvider, this);
			Children = newChildren;
			pageContext.ActiveObject = rightsProvider.InstanceUserRights.HasFlag(InstanceUserRights.ReadUsers) ? newModel : null;
		}

		public Task HandleDoubleClick(CancellationToken cancellationToken) => Refresh(cancellationToken);

		public IReadOnlyList<User> GetUsers() => userProvider.GetUsers()?.Where(x => x.Id != userProvider.CurrentUser.Id && activeUsers?.Any(y => y.UserId == x.Id) != true).ToList();
	}
}