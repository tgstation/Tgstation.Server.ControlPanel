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

		public string Title => "Add User";

		public string Icon => "resm:Tgstation.Server.ControlPanel.Assets.plus.jpg";

		public bool IsExpanded { get; set; }

		public IReadOnlyList<ITreeNode> Children => null;
		public bool IdMode => users == null || users.Count == 0;

		public IReadOnlyList<string> UserStrings { get; }

		public int SelectedIndex { get; set; }

		public uint UserId { get; set; }

		public ICommand Close { get; }

		public EnumCommand<AddInstanceUserCommand> Add { get; }

		readonly PageContextViewModel pageContext;
		readonly InstanceUserRootViewModel instanceUserRootViewModel;
		readonly IInstanceUserClient instanceUserClient;
		readonly IInstanceUserRightsProvider rightsProvider;
		readonly IReadOnlyList<User> users;

		public AddInstanceUserViewModel(PageContextViewModel pageContext, InstanceUserRootViewModel instanceUserRootViewModel, IInstanceUserClient instanceUserClient, IInstanceUserRightsProvider rightsProvider, IUserProvider userProvider)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.instanceUserRootViewModel = instanceUserRootViewModel ?? throw new ArgumentNullException(nameof(instanceUserRootViewModel));
			this.instanceUserClient = instanceUserClient ?? throw new ArgumentNullException(nameof(instanceUserClient));
			this.rightsProvider = rightsProvider ?? throw new ArgumentNullException(nameof(rightsProvider));

			users = userProvider.GetUsers()?.Where(x => x.Id != userProvider.CurrentUser.Id).ToList();
			UserStrings = users?.Select(x => String.Format(CultureInfo.InvariantCulture, "{0} ({1})", x.Name, x.Id)).ToList();

			Close = new EnumCommand<AddInstanceUserCommand>(AddInstanceUserCommand.Close, this);
			Add = new EnumCommand<AddInstanceUserCommand>(AddInstanceUserCommand.Add, this);
		}

		public Task HandleDoubleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(AddInstanceUserCommand command)
		{
			switch (command)
			{
				case AddInstanceUserCommand.Close:
					return true;
				case AddInstanceUserCommand.Add:
					return rightsProvider.InstanceUserRights.HasFlag(InstanceUserRights.WriteUsers);
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public async Task RunCommand(AddInstanceUserCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case AddInstanceUserCommand.Close:
					pageContext.ActiveObject = null;
					break;
				case AddInstanceUserCommand.Add:
					var user = new InstanceUser
					{
						UserId = IdMode ? UserId : users[SelectedIndex].Id,
					};
					var newUser = await instanceUserClient.Create(user, cancellationToken).ConfigureAwait(true);
					instanceUserRootViewModel.DirectAdd(newUser);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}