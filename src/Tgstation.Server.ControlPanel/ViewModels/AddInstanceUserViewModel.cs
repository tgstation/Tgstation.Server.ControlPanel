using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class AddInstanceUserViewModel : ViewModelBase, ITreeNode, ICommandReceiver<AddInstanceUserViewModel.AddInstanceUserCommand>
	{
		public enum AddInstanceUserCommand
		{
			Close,
			Add
		}

		public string Title => "Add User/Group";

		public string Icon => "resm:Tgstation.Server.ControlPanel.Assets.plus.jpg";

		public bool IsExpanded { get; set; }

		public IReadOnlyList<ITreeNode> Children => null;
		public bool IdMode => (users == null || users.Count == 0) && (groups == null || groups.Count == 0);

		public IReadOnlyList<string> UserStrings { get; }

		public int SelectedIndex { get; set; }

		public uint UserId { get; set; }

		public ICommand Close { get; }

		public EnumCommand<AddInstanceUserCommand> Add { get; }

		readonly PageContextViewModel pageContext;
		readonly InstanceUserRootViewModel instanceUserRootViewModel;
		readonly IInstancePermissionSetClient instanceUserClient;
		readonly IInstanceUserRightsProvider rightsProvider;
		readonly IReadOnlyList<User> users;
		readonly IReadOnlyList<UserGroup> groups;

		bool loading;

		public AddInstanceUserViewModel(PageContextViewModel pageContext, InstanceUserRootViewModel instanceUserRootViewModel, IInstancePermissionSetClient instanceUserClient, IInstanceUserRightsProvider rightsProvider, IUserProvider userProvider)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.instanceUserRootViewModel = instanceUserRootViewModel ?? throw new ArgumentNullException(nameof(instanceUserRootViewModel));
			this.instanceUserClient = instanceUserClient ?? throw new ArgumentNullException(nameof(instanceUserClient));
			this.rightsProvider = rightsProvider ?? throw new ArgumentNullException(nameof(rightsProvider));

			users = userProvider.GetUsers()?.Where(x => x.Id != userProvider.CurrentUser.Id).ToList();
			groups = userProvider.GetGroups()?.Where(x => x.Id != userProvider.CurrentUser.Group?.Id).ToList();
			var userStrings = users?.Select(x => string.Format(CultureInfo.InvariantCulture, "User {0} ({1})", x.Name, x.Id)).ToList() ?? new List<string>();
			userStrings.AddRange(groups?.Select(x => $"Group {x.Name} ({x.Id})") ?? Enumerable.Empty<string>());
			if (userStrings.Count > 0)
				UserStrings = userStrings;
				
			rightsProvider.OnUpdated += (a, b) => Add.Recheck();

			Close = new EnumCommand<AddInstanceUserCommand>(AddInstanceUserCommand.Close, this);
			Add = new EnumCommand<AddInstanceUserCommand>(AddInstanceUserCommand.Add, this);
		}

		public Task HandleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(AddInstanceUserCommand command)
		{
			return command switch
			{
				AddInstanceUserCommand.Close => true,
				AddInstanceUserCommand.Add => !loading && rightsProvider.InstanceUserRights.HasFlag(InstancePermissionSetRights.Create),
				_ => throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!"),
			};
		}

		public async Task RunCommand(AddInstanceUserCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case AddInstanceUserCommand.Close:
					pageContext.ActiveObject = null;
					break;
				case AddInstanceUserCommand.Add:
					loading = true;
					Add.Recheck();
					try
					{
						var user = new InstancePermissionSet
						{
							PermissionSetId = IdMode
								? UserId
								: (SelectedIndex >= users.Count
									? groups[SelectedIndex - users.Count].PermissionSet.Id.Value
									: users[SelectedIndex].GetPermissionSet().Id.Value),
						};
						var newUser = await instanceUserClient.Create(user, cancellationToken).ConfigureAwait(true);
						instanceUserRootViewModel.DirectAdd(newUser);
					}
					finally
					{
						loading = false;
						Add.Recheck();
					}
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}