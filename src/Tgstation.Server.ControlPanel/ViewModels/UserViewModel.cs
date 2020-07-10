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
	sealed class UserViewModel : ViewModelBase, ITreeNode, ICommandReceiver<UserViewModel.UserCommand>, IUserRightsProvider
	{
		public enum UserCommand
		{
			Close,
			Refresh,
			Save
		}

		public AdministrationRights AdministrationRights => user.AdministrationRights.Value;
		public InstanceManagerRights InstanceManagerRights => user.InstanceManagerRights.Value;
		public bool IsExpanded { get; set; }

		public User User
		{
			get => user;
			set
			{
				using (DelayChangeNotifications())
				{
					this.RaiseAndSetIfChanged(ref user, value);
					newInstanceManagerRights = user.InstanceManagerRights.Value;
					newAdministrationRights = user.AdministrationRights.Value;

					this.RaisePropertyChanged(nameof(AdminWriteUsers));
					this.RaisePropertyChanged(nameof(AdminReadUsers));
					this.RaisePropertyChanged(nameof(AdminEditPassword));
					this.RaisePropertyChanged(nameof(AdminChangeVersion));
					this.RaisePropertyChanged(nameof(AdminRestartServer));
					this.RaisePropertyChanged(nameof(AdminLogs));

					this.RaisePropertyChanged(nameof(InstanceConfig));
					this.RaisePropertyChanged(nameof(InstanceCreate));
					this.RaisePropertyChanged(nameof(InstanceDelete));
					this.RaisePropertyChanged(nameof(InstanceOnline));
					this.RaisePropertyChanged(nameof(InstanceList));
					this.RaisePropertyChanged(nameof(InstanceRead));
					this.RaisePropertyChanged(nameof(InstanceRelocate));
					this.RaisePropertyChanged(nameof(InstanceRename));
					this.RaisePropertyChanged(nameof(InstanceUpdate));
					this.RaisePropertyChanged(nameof(InstanceChatLimit));
					this.RaisePropertyChanged(nameof(InstanceGrant));

					this.RaisePropertyChanged(nameof(IsSystemUser));
				}
			}
		}

		public bool IsSystemUser => User.SystemIdentifier != null;

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

		public EnumCommand<UserCommand> Save { get; }

		public IReadOnlyList<ITreeNode> Children => null;

		public bool Enabled
		{
			get => enabled;
			set => this.RaiseAndSetIfChanged(ref enabled, value);
		}

		public bool AdminWriteUsers
		{
			get => newAdministrationRights.HasFlag(AdministrationRights.WriteUsers);
			set
			{
				var right = AdministrationRights.WriteUsers;
				if (value)
					newAdministrationRights |= right;
				else
					newAdministrationRights &= ~right;
			}
		}
		public bool AdminReadUsers
		{
			get => newAdministrationRights.HasFlag(AdministrationRights.ReadUsers);
			set
			{
				var right = AdministrationRights.ReadUsers;
				if (value)
					newAdministrationRights |= right;
				else
					newAdministrationRights &= ~right;
			}
		}
		public bool AdminEditPassword
		{
			get => newAdministrationRights.HasFlag(AdministrationRights.EditOwnPassword);
			set
			{
				var right = AdministrationRights.EditOwnPassword;
				if (value)
					newAdministrationRights |= right;
				else
					newAdministrationRights &= ~right;
			}
		}

		public bool AdminRestartServer
		{
			get => newAdministrationRights.HasFlag(AdministrationRights.RestartHost);
			set
			{
				var right = AdministrationRights.RestartHost;
				if (value)
					newAdministrationRights |= right;
				else
					newAdministrationRights &= ~right;
			}
		}

		public bool AdminChangeVersion
		{
			get => newAdministrationRights.HasFlag(AdministrationRights.ChangeVersion);
			set
			{
				var right = AdministrationRights.ChangeVersion;
				if (value)
					newAdministrationRights |= right;
				else
					newAdministrationRights &= ~right;
			}
		}
		public bool AdminLogs
		{
			get => newAdministrationRights.HasFlag(AdministrationRights.DownloadLogs);
			set
			{
				var right = AdministrationRights.DownloadLogs;
				if (value)
					newAdministrationRights |= right;
				else
					newAdministrationRights &= ~right;
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

		public bool InstanceCreate
		{
			get => newInstanceManagerRights.HasFlag(InstanceManagerRights.Create);
			set
			{
				var right = InstanceManagerRights.Create;
				if (value)
					newInstanceManagerRights |= right;
				else
					newInstanceManagerRights &= ~right;
			}
		}
		public bool InstanceRead
		{
			get => newInstanceManagerRights.HasFlag(InstanceManagerRights.Read);
			set
			{
				var right = InstanceManagerRights.Read;
				if (value)
					newInstanceManagerRights |= right;
				else
					newInstanceManagerRights &= ~right;
			}
		}
		public bool InstanceRename
		{
			get => newInstanceManagerRights.HasFlag(InstanceManagerRights.Rename);
			set
			{
				var right = InstanceManagerRights.Rename;
				if (value)
					newInstanceManagerRights |= right;
				else
					newInstanceManagerRights &= ~right;
			}
		}
		public bool InstanceRelocate
		{
			get => newInstanceManagerRights.HasFlag(InstanceManagerRights.Relocate);
			set
			{
				var right = InstanceManagerRights.Relocate;
				if (value)
					newInstanceManagerRights |= right;
				else
					newInstanceManagerRights &= ~right;
			}
		}
		public bool InstanceOnline
		{
			get => newInstanceManagerRights.HasFlag(InstanceManagerRights.SetOnline);
			set
			{
				var right = InstanceManagerRights.SetOnline;
				if (value)
					newInstanceManagerRights |= right;
				else
					newInstanceManagerRights &= ~right;
			}
		}
		public bool InstanceDelete
		{
			get => newInstanceManagerRights.HasFlag(InstanceManagerRights.Delete);
			set
			{
				var right = InstanceManagerRights.Delete;
				if (value)
					newInstanceManagerRights |= right;
				else
					newInstanceManagerRights &= ~right;
			}
		}
		public bool InstanceList
		{
			get => newInstanceManagerRights.HasFlag(InstanceManagerRights.List);
			set
			{
				var right = InstanceManagerRights.List;
				if (value)
					newInstanceManagerRights |= right;
				else
					newInstanceManagerRights &= ~right;
			}
		}
		public bool InstanceConfig
		{
			get => newInstanceManagerRights.HasFlag(InstanceManagerRights.SetConfiguration);
			set
			{
				var right = InstanceManagerRights.SetConfiguration;
				if (value)
					newInstanceManagerRights |= right;
				else
					newInstanceManagerRights &= ~right;
			}
		}
		public bool InstanceUpdate
		{
			get => newInstanceManagerRights.HasFlag(InstanceManagerRights.SetAutoUpdate);
			set
			{
				var right = InstanceManagerRights.SetAutoUpdate;
				if (value)
					newInstanceManagerRights |= right;
				else
					newInstanceManagerRights &= ~right;
			}
		}
		public bool InstanceChatLimit
		{
			get => newInstanceManagerRights.HasFlag(InstanceManagerRights.SetChatBotLimit);
			set
			{
				var right = InstanceManagerRights.SetChatBotLimit;
				if (value)
					newInstanceManagerRights |= right;
				else
					newInstanceManagerRights &= ~right;
			}
		}
		public bool InstanceGrant
		{
			get => newInstanceManagerRights.HasFlag(InstanceManagerRights.GrantPermissions);
			set
			{
				var right = InstanceManagerRights.GrantPermissions;
				if (value)
					newInstanceManagerRights |= right;
				else
					newInstanceManagerRights &= ~right;
			}
		}

		public string PasswordLength => $"Minimum Length: {serverInformation.MinimumPasswordLength}";

		public event EventHandler OnUpdated;

		readonly ServerInformation serverInformation;
		readonly IUserRightsProvider userRightsProvider;
		readonly IUsersClient usersClient;
		readonly PageContextViewModel pageContext;

		InstanceManagerRights newInstanceManagerRights;
		AdministrationRights newAdministrationRights;

		User user;

		bool refreshing;
		bool error;
		bool canEditPassword;
		bool canEditRights;
		bool enabled;

		string newPassword;
		string passwordConfirm;

		public UserViewModel(IUsersClient usersClient, ServerInformation serverInformation, User user, PageContextViewModel pageContext, IUserRightsProvider userRightsProvider)
		{
			this.usersClient = usersClient ?? throw new ArgumentNullException(nameof(usersClient));
			this.serverInformation = serverInformation ?? throw new ArgumentNullException(nameof(serverInformation));
			User = user ?? throw new ArgumentNullException(nameof(user));
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.userRightsProvider = userRightsProvider ?? this;

			Close = new EnumCommand<UserCommand>(UserCommand.Close, this);
			Refresh = new EnumCommand<UserCommand>(UserCommand.Refresh, this);
			Save = new EnumCommand<UserCommand>(UserCommand.Save, this);

			NewPassword = String.Empty;
			PasswordConfirm = String.Empty;

			Enabled = User.Enabled.Value;
			void SetLocks()
			{
				CanEditRights = this.userRightsProvider.AdministrationRights.HasFlag(AdministrationRights.WriteUsers);
				CanEditPassword = CanEditRights || (this.userRightsProvider.AdministrationRights.HasFlag(AdministrationRights.EditOwnPassword) && this.userRightsProvider == this);
			};
			SetLocks();

			this.userRightsProvider.OnUpdated += (a, b) =>
			{
				using (DelayChangeNotifications())
					SetLocks();
			};
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
				case UserCommand.Save:
					var update = new UserUpdate
					{
						Id = User.Id,
						AdministrationRights = newAdministrationRights,
						InstanceManagerRights = newInstanceManagerRights,
						Enabled = Enabled
					};
					if (NewPassword.Length > 0)
						update.Password = NewPassword;
					await RunRequest(async () => User = await usersClient.Update(update, cancellationToken).ConfigureAwait(false)).ConfigureAwait(true);
					OnUpdated?.Invoke(this, new EventArgs());
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}

			using (DelayChangeNotifications())
			{
				NewPassword = String.Empty;
				PasswordConfirm = String.Empty;
				Refresh.Recheck();
				Save.Recheck();
			}
		}
	}
}