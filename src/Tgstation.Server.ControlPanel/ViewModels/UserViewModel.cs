using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class UserViewModel : ViewModelBase, ITreeNode, ICommandReceiver<UserViewModel.UserCommand>
	{
		public enum UserCommand
		{
			Close,
			Refresh,
			Save
		}

		public User User
		{
			get => user;
			set
			{
				using (DelayChangeNotifications())
				{
					this.RaiseAndSetIfChanged(ref user, value);
					this.RaisePropertyChanged(nameof(AdminEditUsers));
					this.RaisePropertyChanged(nameof(AdminEditPassword));
					this.RaisePropertyChanged(nameof(AdminChangeVersion));
					this.RaisePropertyChanged(nameof(AdminRestartServer));
				}
			}
		}

		public string FormatCreatedBy => User.CreatedBy != null ? String.Format(CultureInfo.InvariantCulture, "{0} ({1})", User.CreatedBy.Name, User.CreatedBy.Id) : "TGS";

		public string Title => String.Format(CultureInfo.InvariantCulture, "User: {0} ({1})", user.Name, user.Id);

		public string Icon => "resm:Tgstation.Server.ControlPanel.Assets.user.png";

		public bool Refreshing
		{
			get => refreshing;
			set => this.RaiseAndSetIfChanged(ref refreshing, value);
		}
		public bool Error
		{
			get => error;
			set => this.RaiseAndSetIfChanged(ref error, value);
		}

		public string NewPassword
		{
			get => newPassword;
			set => this.RaiseAndSetIfChanged(ref newPassword, value);
		}
		public string PasswordConfirm
		{
			get => passwordConfirm;
			set => this.RaiseAndSetIfChanged(ref passwordConfirm, value);
		}

		public ICommand Close { get; }

		public EnumCommand<UserCommand> Refresh { get; }

		public IReadOnlyList<ITreeNode> Children => null;

		public bool AdminEditUsers
		{
			get => user.AdministrationRights.Value.HasFlag(AdministrationRights.EditUsers);
			set
			{
				if (value)
					user.AdministrationRights |= AdministrationRights.EditUsers;
				else
					user.AdministrationRights &= ~AdministrationRights.EditUsers;
				this.RaisePropertyChanged(nameof(AdminEditUsers));
				this.RaisePropertyChanged(nameof(CanEditPassword));
			}
		}
		public bool AdminEditPassword
		{
			get => user.AdministrationRights.Value.HasFlag(AdministrationRights.EditPassword);
			set
			{
				if (value)
					user.AdministrationRights |= AdministrationRights.EditPassword;
				else
					user.AdministrationRights &= ~AdministrationRights.EditPassword;
				this.RaisePropertyChanged(nameof(AdminEditPassword));
				this.RaisePropertyChanged(nameof(CanEditPassword));
			}
		}

		public bool AdminRestartServer
		{
			get => user.AdministrationRights.Value.HasFlag(AdministrationRights.RestartHost);
			set
			{
				if (value)
					user.AdministrationRights |= AdministrationRights.RestartHost;
				else
					user.AdministrationRights &= ~AdministrationRights.RestartHost;
				this.RaisePropertyChanged(nameof(AdminRestartServer));
			}
		}

		public bool AdminChangeVersion
		{
			get => user.AdministrationRights.Value.HasFlag(AdministrationRights.ChangeVersion);
			set
			{
				if (value)
					user.AdministrationRights |= AdministrationRights.ChangeVersion;
				else
					user.AdministrationRights &= ~AdministrationRights.ChangeVersion;
				this.RaisePropertyChanged(nameof(AdminChangeVersion));
			}
		}

		public bool CanEditPassword
		{
			get => canEditPassword;
			set => this.RaiseAndSetIfChanged(ref canEditPassword, value);
		}

		public bool CanEditRights
		{
			get => canEditRights;
			set => this.RaiseAndSetIfChanged(ref canEditRights, value);
		}

		readonly IUsersClient usersClient;
		readonly PageContextViewModel pageContext;

		User user;

		bool refreshing;
		bool error;
		bool canEditPassword;
		bool canEditRights;


		string newPassword;
		string passwordConfirm;

		public UserViewModel(IUsersClient usersClient, User user, PageContextViewModel pageContext)
		{
			this.usersClient = usersClient ?? throw new ArgumentNullException(nameof(usersClient));
			this.user = user ?? throw new ArgumentNullException(nameof(user));
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));

			Close = new EnumCommand<UserCommand>(UserCommand.Close, this);
			Refresh = new EnumCommand<UserCommand>(UserCommand.Refresh, this);

			NewPassword = String.Empty;
			PasswordConfirm = String.Empty;

			CanEditPassword = AdminEditUsers || AdminEditPassword;
			CanEditRights = AdminEditUsers;
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
				case UserCommand.Save:
					return NewPassword == PasswordConfirm;
				case UserCommand.Refresh:
					return !Refreshing;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public async Task RunCommand(UserCommand command, CancellationToken cancellationToken)
		{
			async Task RunRequest(Func<Task> action)
			{
				Refreshing = true;
				Refresh.Recheck();
				try
				{
					await action().ConfigureAwait(true);
				}
				catch (ClientException)
				{
					Error = true;
				}
				catch (HttpRequestException)
				{
					Error = true;
				}

				Refreshing = false;
				Refresh.Recheck();
			}

			switch (command)
			{
				case UserCommand.Close:
					pageContext.ActiveObject = null;
					break;
				case UserCommand.Refresh:
					await RunRequest(async () => user = await usersClient.GetId(user, cancellationToken).ConfigureAwait(true)).ConfigureAwait(false);
					break;
				case UserCommand.Save:
					var update = new UserUpdate
					{
						AdministrationRights = user.AdministrationRights,
						InstanceManagerRights = user.InstanceManagerRights,
						Enabled = user.Enabled,
						Password = NewPassword
					};
					await RunRequest(async () => user = await usersClient.Update(update, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}

			CanEditPassword = AdminEditUsers || AdminEditPassword;
			CanEditRights = AdminEditUsers;
			NewPassword = String.Empty;
			PasswordConfirm = String.Empty;
		}
	}
}