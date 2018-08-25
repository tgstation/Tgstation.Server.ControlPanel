using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Client;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class UserViewModel : ViewModelBase, ITreeNode, ICommandReceiver<UserViewModel.UserCommand>
	{
		public enum UserCommand
		{
			Close
		}

		public User User
		{
			get => user;
			set
			{
				using (DelayChangeNotifications())
				{
					this.RaiseAndSetIfChanged(ref user, value);
				}
			}
		}

		public string FormatCreatedBy => User.CreatedBy != null ? String.Format(CultureInfo.InvariantCulture, "{0} ({1})", User.CreatedBy.Name, User.CreatedBy.Id) : "TGS";

		public string Title => String.Format(CultureInfo.InvariantCulture, "User: {0} ({1})", user.Name, user.Id);

		public string Icon => "resm:Tgstation.Server.ControlPanel.Assets.user.png";

		public ICommand Close { get; }

		public IReadOnlyList<ITreeNode> Children => null;

		readonly IUsersClient usersClient;
		readonly PageContextViewModel pageContext;

		User user;

		public UserViewModel(IUsersClient usersClient, User user, PageContextViewModel pageContext)
		{
			this.usersClient = usersClient ?? throw new ArgumentNullException(nameof(usersClient));
			this.user = user ?? throw new ArgumentNullException(nameof(user));
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));

			Close = new EnumCommand<UserCommand>(UserCommand.Close, this);
		}

		public Task HandleDoubleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(UserCommand command)
		{
			switch (command)
			{
				case UserCommand.Close:
					return true;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public Task RunCommand(UserCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case UserCommand.Close:
					pageContext.ActiveObject = null;
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
			return Task.CompletedTask;
		}
	}
}