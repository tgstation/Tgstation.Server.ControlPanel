using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class InstanceUserRootViewModel : ViewModelBase, ITreeNode
	{
		public string Title => "Users/Groups";

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
		readonly IInstancePermissionSetClient instanceUserClient;
		readonly IInstanceUserRightsProvider rightsProvider;
		readonly IUserProvider userProvider;
		readonly InstanceViewModel instanceViewModel;

		IReadOnlyList<ITreeNode> children;
		IReadOnlyList<InstancePermissionSet> activeUsers;

		bool loading;
		string icon;


		public InstanceUserRootViewModel(PageContextViewModel pageContext, IInstancePermissionSetClient instanceUserClient, IInstanceUserRightsProvider rightsProvider, IUserProvider userProvider, InstanceViewModel instanceViewModel)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.instanceUserClient = instanceUserClient ?? throw new ArgumentNullException(nameof(instanceUserClient));
			this.rightsProvider = rightsProvider ?? throw new ArgumentNullException(nameof(rightsProvider));
			this.userProvider = userProvider ?? throw new ArgumentNullException(nameof(userProvider));
			this.instanceViewModel = instanceViewModel ?? throw new ArgumentNullException(nameof(instanceViewModel));

			Icon = "resm:Tgstation.Server.ControlPanel.Assets.folder.png";

			async void InitialLoad() => await Refresh(default).ConfigureAwait(false);
			InitialLoad();

			rightsProvider.OnUpdated += (a, b) => InitialLoad();
		}

		public static string GetDisplayNameForInstanceUser(IUserProvider userProvider, InstancePermissionSet user)
		{
			var actualUser = userProvider
				.GetUsers()
				?.Where(x => x.GetPermissionSet().Id == user.PermissionSetId)
				.FirstOrDefault();

			var actualGroup = userProvider
				.GetGroups()
				?.Where(x => x.PermissionSet.Id == user.PermissionSetId)
				.FirstOrDefault();

			return actualUser == null
				? actualGroup == null
					? $"Permission Set ID {user.PermissionSetId}"
					: $"Group {actualGroup.Name} ({actualGroup.Id})"
				: $"User {actualUser.Name} ({actualUser.Id})";
		}

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
				if (rightsProvider.InstanceUserRights == InstancePermissionSetRights.None)
				{
					Children = null;
					Icon = "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg";
					return;
				}

				Children = null;
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png";
				this.RaisePropertyChanged(nameof(Icon));

				var newChildren = new List<ITreeNode>();

				var hasReadRight = rightsProvider.InstanceUserRights.HasFlag(InstancePermissionSetRights.Read);

				if (hasReadRight)
					activeUsers = (await instanceUserClient.List(null, cancellationToken).ConfigureAwait(true)).Where(x => x.PermissionSetId != userProvider.CurrentUser.GetPermissionSet().Id).ToList();

				if (rightsProvider.InstanceUserRights.HasFlag(InstancePermissionSetRights.Create))
					newChildren.Add(new AddInstanceUserViewModel(pageContext, this, instanceUserClient, rightsProvider, userProvider));

				if (hasReadRight)
					newChildren.AddRange(activeUsers
						.Select(x => new InstanceUserViewModel(pageContext, instanceViewModel, rightsProvider, instanceUserClient, x,
						GetDisplayNameForInstanceUser(userProvider, x),
						rightsProvider, this, userProvider.GetGroups()?.Any(y => y.PermissionSet.Id == x.PermissionSetId) == true)));

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

		public void DirectAdd(InstancePermissionSet user)
		{
			var newModel = new InstanceUserViewModel(pageContext, instanceViewModel, rightsProvider, instanceUserClient, user, GetDisplayNameForInstanceUser(userProvider, user), rightsProvider, this, (userProvider.GetGroups()?.Any(y => y.PermissionSet.Id == user.PermissionSetId) == true));
			var newChildren = new List<ITreeNode>(Children)
			{
				newModel
			};
			activeUsers = new List<InstancePermissionSet>(activeUsers)
			{
				user
			};
			if (newChildren[0] is AddInstanceUserViewModel)
				newChildren[0] = new AddInstanceUserViewModel(pageContext, this, instanceUserClient, rightsProvider, userProvider);
			Children = newChildren;
			pageContext.ActiveObject = rightsProvider.InstanceUserRights.HasFlag(InstancePermissionSetRights.Read) ? newModel : null;
		}

		public Task HandleClick(CancellationToken cancellationToken) => Refresh(cancellationToken);
	}
}