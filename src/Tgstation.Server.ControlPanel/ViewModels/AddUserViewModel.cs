using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Client;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class AddUserViewModel : ViewModelBase, ITreeNode, ICommandReceiver<AddUserViewModel.AddUserCommand>
	{
		public enum AddUserCommand
		{
			Close,
			Add
		}

		public string Title => "Add User";
		public bool IsExpanded { get; set; }

		public string Icon => "resm:Tgstation.Server.ControlPanel.Assets.plus.jpg";
		
		public string Username
		{
			get => username;
			set
			{
				this.RaiseAndSetIfChanged(ref username, value);
				Add.Recheck();
			}
		}

		public string Password
		{
			get => password;
			set
			{
				this.RaiseAndSetIfChanged(ref password, value);
				Add.Recheck();
			}
		}

		public string ConfirmPassword
		{
			get => confirmPassword;
			set
			{
				this.RaiseAndSetIfChanged(ref confirmPassword, value);
				Add.Recheck();
			}
		}

		public string PasswordLength => $"Minimum Length: {serverInformation.MinimumPasswordLength}";

		public string SystemIdentifier
		{
			get => systemIdentifier;
			set
			{
				this.RaiseAndSetIfChanged(ref systemIdentifier, value);
				Add.Recheck();
			}
		}

		public IReadOnlyList<ITreeNode> Children => null;

		public ICommand Close { get; }
		public EnumCommand<AddUserCommand> Add { get; }

		readonly PageContextViewModel pageContext;
		readonly ServerInformation serverInformation;
		readonly IUsersClient usersClient;
		readonly UsersRootViewModel usersRootViewModel;

		string username;
		string password;
		string confirmPassword;
		string systemIdentifier;

		public AddUserViewModel(PageContextViewModel pageContext, ServerInformation serverInformation, IUsersClient usersClient, UsersRootViewModel usersRootViewModel)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.serverInformation = serverInformation ?? throw new ArgumentNullException(nameof(serverInformation));
			this.usersClient = usersClient ?? throw new ArgumentNullException(nameof(usersClient));
			this.usersRootViewModel = usersRootViewModel ?? throw new ArgumentNullException(nameof(usersRootViewModel));

			Close = new EnumCommand<AddUserCommand>(AddUserCommand.Close, this);
			Add = new EnumCommand<AddUserCommand>(AddUserCommand.Add, this);
		}

		public Task HandleClick(CancellationToken cancellationToken)
		{
			Username = String.Empty;
			Password = String.Empty;
			ConfirmPassword = String.Empty;
			SystemIdentifier = String.Empty;
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(AddUserCommand command)
		{
			switch (command)
			{
				case AddUserCommand.Close:
					return true;
				case AddUserCommand.Add:
					return ((Username.Length > 0 && Password.Length > serverInformation.MinimumPasswordLength) ^ (SystemIdentifier.Length > 0)) && Password == ConfirmPassword;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public async Task RunCommand(AddUserCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case AddUserCommand.Close:
					pageContext.ActiveObject = null;
					break;
				case AddUserCommand.Add:
					UserUpdate uu;
					if (SystemIdentifier.Length != 0)
						uu = new UserUpdate
						{
							SystemIdentifier = SystemIdentifier
						};
					else
						uu = new UserUpdate
						{
							Name = Username,
							Password = Password
						};
					try
					{
						var newUser = await usersClient.Create(uu, cancellationToken).ConfigureAwait(true);
						usersRootViewModel.DirectAdd(newUser);
					}
					catch { }
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}