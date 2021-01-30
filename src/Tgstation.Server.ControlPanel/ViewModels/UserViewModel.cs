using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class UserViewModel : ViewModelBase, ITreeNode, ICommandReceiver<UserViewModel.UserCommand>, IUserRightsProvider
	{
		public enum UserCommand
		{
			Close,
			Refresh,
			RemoveFromGroup,
			Save
		}

		public AdministrationRights AdministrationRights => user.GetPermissionSet().AdministrationRights.Value;
		public InstanceManagerRights InstanceManagerRights => user.GetPermissionSet().InstanceManagerRights.Value;
		public bool IsExpanded { get; set; }

		public User User
		{
			get => user;
			set
			{
				using (DelayChangeNotifications())
				{
					this.RaiseAndSetIfChanged(ref user, value);
					this.RaisePropertyChanged(nameof(IsSystemUser));
					UpdatePermissionSet();
				}
			}
		}

		public bool IsSystemUser => User.SystemIdentifier != null;

		public string FormatCreatedBy => User.CreatedBy != null ? string.Format(CultureInfo.InvariantCulture, "{0} ({1})", User.CreatedBy.Name, User.CreatedBy.Id) : "TGS";

		public string Title => string.Format(CultureInfo.InvariantCulture, "User: {0} ({1})", user.Name, user.Id);

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
			set
			{
				this.RaiseAndSetIfChanged(ref newPassword, value);
				Save.Recheck();
			}
		}
		public string PasswordConfirm
		{
			get => passwordConfirm;
			set
			{
				this.RaiseAndSetIfChanged(ref passwordConfirm, value);
				Save.Recheck();
			}
		}

		public ICommand Close { get; }

		public EnumCommand<UserCommand> Refresh { get; }

		public EnumCommand<UserCommand> RemoveFromGroup { get; }

		public EnumCommand<UserCommand> Save { get; }

		public IReadOnlyList<ITreeNode> Children => null;

		public bool Enabled
		{
			get => enabled;
			set => this.RaiseAndSetIfChanged(ref enabled, value);
		}

		public bool CanEditPassword
		{
			get => canEditPassword;
			set => this.RaiseAndSetIfChanged(ref canEditPassword, value);
		}

		public bool IsGroupedUser => User.Group != null;

		public PermissionSetViewModel PermissionSetViewModel { get; private set; }

		public string PasswordLength => $"Minimum Length: {serverInformation.MinimumPasswordLength}";

		public event EventHandler OnUpdated;

		readonly ServerInformation serverInformation;
		readonly IUserRightsProvider userRightsProvider;
		readonly IUsersClient usersClient;
		readonly PageContextViewModel pageContext;

		User user;

		bool refreshing;
		bool error;
		bool canEditPassword;
		bool enabled;

		string newPassword;
		string passwordConfirm;

		public UserViewModel(IUsersClient usersClient, ServerInformation serverInformation, User user, PageContextViewModel pageContext, IUserRightsProvider userRightsProvider)
		{
			this.usersClient = usersClient ?? throw new ArgumentNullException(nameof(usersClient));
			this.serverInformation = serverInformation ?? throw new ArgumentNullException(nameof(serverInformation));
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.userRightsProvider = userRightsProvider ?? this;

			User = user ?? throw new ArgumentNullException(nameof(user));

			Close = new EnumCommand<UserCommand>(UserCommand.Close, this);
			Refresh = new EnumCommand<UserCommand>(UserCommand.Refresh, this);
			RemoveFromGroup = new EnumCommand<UserCommand>(UserCommand.RemoveFromGroup, this);
			Save = new EnumCommand<UserCommand>(UserCommand.Save, this);

			NewPassword = string.Empty;
			PasswordConfirm = string.Empty;

			Enabled = User.Enabled.Value;
			void SetLocks()
			{
				CanEditPassword = this.userRightsProvider.AdministrationRights.HasFlag(AdministrationRights.WriteUsers) || (this.userRightsProvider.AdministrationRights.HasFlag(AdministrationRights.EditOwnPassword) && this.userRightsProvider == this);
			};

			this.userRightsProvider.OnUpdated += (a, b) =>
			{
				using (DelayChangeNotifications())
					SetLocks();
			};

			UpdatePermissionSet();
		}

		public Task HandleClick(CancellationToken cancellationToken)
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
				case UserCommand.RemoveFromGroup:
					return User.Group != null;
				case UserCommand.Save:
					var changedPassword = (NewPassword?.Length ?? 0 + PasswordConfirm?.Length ?? 0) > 0;
					if (changedPassword)
					{
						var validPassword = changedPassword && NewPassword == PasswordConfirm && (NewPassword?.Length ?? 0) >= serverInformation.MinimumPasswordLength;

						if (!CanEditPassword || !validPassword)
							return false;
					}

					goto case UserCommand.Refresh;
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
				Save.Recheck();

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
			}

			switch (command)
			{
				case UserCommand.Close:
					pageContext.ActiveObject = null;
					break;
				case UserCommand.Refresh:
					await RunRequest(async () => User = await usersClient.GetId(user, cancellationToken).ConfigureAwait(true)).ConfigureAwait(true);
					OnUpdated?.Invoke(this, new EventArgs());
					break;
				case UserCommand.RemoveFromGroup:
					var update = new UserUpdate
					{
						Id = User.Id,
						PermissionSet = new PermissionSet
						{
							AdministrationRights = User.Group.PermissionSet.AdministrationRights,
							InstanceManagerRights = User.Group.PermissionSet.InstanceManagerRights,
						}
					};
					await RunRequest(async () => User = await usersClient.Update(update, cancellationToken).ConfigureAwait(false)).ConfigureAwait(true);
					UpdatePermissionSet();
					break;
				case UserCommand.Save:
					var update2 = new UserUpdate
					{
						Id = User.Id,
						Enabled = Enabled
					};
					if (NewPassword.Length > 0)
						update2.Password = NewPassword;
					await RunRequest(async () => User = await usersClient.Update(update2, cancellationToken).ConfigureAwait(false)).ConfigureAwait(true);
					UpdatePermissionSet();
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}

			using (DelayChangeNotifications())
			{
				NewPassword = string.Empty;
				PasswordConfirm = string.Empty;
				Refresh.Recheck();
				Save.Recheck();
			}
		}

		void UpdatePermissionSet()
		{
			if (User.PermissionSet != null)
				PermissionSetViewModel = new PermissionSetViewModel(userRightsProvider, usersClient, User);
			else
				PermissionSetViewModel = null;

			this.RaisePropertyChanged(nameof(PermissionSetViewModel));
			this.RaisePropertyChanged(nameof(IsGroupedUser));

			OnUpdated?.Invoke(this, new EventArgs());
		}
	}
}
