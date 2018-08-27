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

					this.RaisePropertyChanged(nameof(AdminEditUsers));
					this.RaisePropertyChanged(nameof(AdminEditPassword));
					this.RaisePropertyChanged(nameof(AdminChangeVersion));
					this.RaisePropertyChanged(nameof(AdminRestartServer));

					this.RaisePropertyChanged(nameof(InstanceConfig));
					this.RaisePropertyChanged(nameof(InstanceCreate));
					this.RaisePropertyChanged(nameof(InstanceDelete));
					this.RaisePropertyChanged(nameof(InstanceOnline));
					this.RaisePropertyChanged(nameof(InstanceList));
					this.RaisePropertyChanged(nameof(InstanceRead));
					this.RaisePropertyChanged(nameof(InstanceRelocate));
					this.RaisePropertyChanged(nameof(InstanceRename));
					this.RaisePropertyChanged(nameof(InstanceUpdate));
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

		public bool AdminEditUsers
		{
			get => user.AdministrationRights.Value.HasFlag(AdministrationRights.EditUsers);
			set
			{
				var right = AdministrationRights.EditUsers;
				if (value)
					user.AdministrationRights |= right;
				else
					user.AdministrationRights &= ~right;
			}
		}
		public bool AdminEditPassword
		{
			get => user.AdministrationRights.Value.HasFlag(AdministrationRights.EditPassword);
			set
			{
				var right = AdministrationRights.EditPassword;
				if (value)
					user.AdministrationRights |= right;
				else
					user.AdministrationRights &= ~right;
			}
		}

		public bool AdminRestartServer
		{
			get => user.AdministrationRights.Value.HasFlag(AdministrationRights.RestartHost);
			set
			{
				var right = AdministrationRights.RestartHost;
				if (value)
					user.AdministrationRights |= right;
				else
					user.AdministrationRights &= ~right;
			}
		}

		public bool AdminChangeVersion
		{
			get => user.AdministrationRights.Value.HasFlag(AdministrationRights.ChangeVersion);
			set
			{
				var right = AdministrationRights.ChangeVersion;
				if (value)
					user.AdministrationRights |= right;
				else
					user.AdministrationRights &= ~right;
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
			get => user.InstanceManagerRights.Value.HasFlag(InstanceManagerRights.Create);
			set
			{
				var right = InstanceManagerRights.Create;
				if (value)
					user.InstanceManagerRights |= right;
				else
					user.InstanceManagerRights &= ~right;
			}
		}
		public bool InstanceRead
		{
			get => user.InstanceManagerRights.Value.HasFlag(InstanceManagerRights.Read);
			set
			{
				var right = InstanceManagerRights.Read;
				if (value)
					user.InstanceManagerRights |= right;
				else
					user.InstanceManagerRights &= ~right;
			}
		}
		public bool InstanceRename
		{
			get => user.InstanceManagerRights.Value.HasFlag(InstanceManagerRights.Rename);
			set
			{
				var right = InstanceManagerRights.Rename;
				if (value)
					user.InstanceManagerRights |= right;
				else
					user.InstanceManagerRights &= ~right;
			}
		}
		public bool InstanceRelocate
		{
			get => user.InstanceManagerRights.Value.HasFlag(InstanceManagerRights.Relocate);
			set
			{
				var right = InstanceManagerRights.Relocate;
				if (value)
					user.InstanceManagerRights |= right;
				else
					user.InstanceManagerRights &= ~right;
			}
		}
		public bool InstanceOnline
		{
			get => user.InstanceManagerRights.Value.HasFlag(InstanceManagerRights.SetOnline);
			set
			{
				var right = InstanceManagerRights.SetOnline;
				if (value)
					user.InstanceManagerRights |= right;
				else
					user.InstanceManagerRights &= ~right;
			}
		}
		public bool InstanceDelete
		{
			get => user.InstanceManagerRights.Value.HasFlag(InstanceManagerRights.Delete);
			set
			{
				var right = InstanceManagerRights.Delete;
				if (value)
					user.InstanceManagerRights |= right;
				else
					user.InstanceManagerRights &= ~right;
			}
		}
		public bool InstanceList
		{
			get => user.InstanceManagerRights.Value.HasFlag(InstanceManagerRights.List);
			set
			{
				var right = InstanceManagerRights.List;
				if (value)
					user.InstanceManagerRights |= right;
				else
					user.InstanceManagerRights &= ~right;
			}
		}
		public bool InstanceConfig
		{
			get => user.InstanceManagerRights.Value.HasFlag(InstanceManagerRights.SetConfiguration);
			set
			{
				var right = InstanceManagerRights.SetConfiguration;
				if (value)
					user.InstanceManagerRights |= right;
				else
					user.InstanceManagerRights &= ~right;
			}
		}
		public bool InstanceUpdate
		{
			get => user.InstanceManagerRights.Value.HasFlag(InstanceManagerRights.SetAutoUpdate);
			set
			{
				var right = InstanceManagerRights.SetAutoUpdate;
				if (value)
					user.InstanceManagerRights |= right;
				else
					user.InstanceManagerRights &= ~right;
			}
		}

		readonly IUserRightsProvider userRightsProvider;
		readonly IUsersClient usersClient;
		readonly PageContextViewModel pageContext;

		User user;

		bool refreshing;
		bool error;
		bool canEditPassword;
		bool canEditRights;
		bool enabled;

		string newPassword;
		string passwordConfirm;

		public UserViewModel(IUsersClient usersClient, User user, PageContextViewModel pageContext, IUserRightsProvider userRightsProvider)
		{
			this.usersClient = usersClient ?? throw new ArgumentNullException(nameof(usersClient));
			this.user = user ?? throw new ArgumentNullException(nameof(user));
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.userRightsProvider = userRightsProvider ?? this;

			Close = new EnumCommand<UserCommand>(UserCommand.Close, this);
			Refresh = new EnumCommand<UserCommand>(UserCommand.Refresh, this);
			Save = new EnumCommand<UserCommand>(UserCommand.Save, this);

			NewPassword = String.Empty;
			PasswordConfirm = String.Empty;

			Enabled = User.Enabled.Value;
			CanEditRights = this.userRightsProvider.AdministrationRights.HasFlag(AdministrationRights.EditUsers);
			CanEditPassword = CanEditRights || this.userRightsProvider.AdministrationRights.HasFlag(AdministrationRights.EditPassword);
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
					if (NewPassword != PasswordConfirm || !CanEditPassword)
						return false;
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
					break;
				case UserCommand.Save:
					var update = new UserUpdate
					{
						Id = User.Id,
						AdministrationRights = User.AdministrationRights,
						InstanceManagerRights = User.InstanceManagerRights,
						Enabled = Enabled
					};
					if (NewPassword.Length > 0)
						update.Password = NewPassword;
					await RunRequest(async () => user = await usersClient.Update(update, cancellationToken).ConfigureAwait(false)).ConfigureAwait(true);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}

			using (DelayChangeNotifications())
			{
				if (userRightsProvider == this)
				{
					CanEditPassword = AdminEditUsers || AdminEditPassword;
					CanEditRights = AdminEditUsers;
				}
				NewPassword = String.Empty;
				PasswordConfirm = String.Empty;
				Refresh.Recheck();
				Save.Recheck();
			}
		}
	}
}