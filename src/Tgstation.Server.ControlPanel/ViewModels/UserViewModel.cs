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
			Refresh
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

		public ICommand Close { get; }

		public EnumCommand<UserCommand> Refresh { get; }

		public IReadOnlyList<ITreeNode> Children => null;

		public bool AdminEditUsers => user.AdministrationRights.Value.HasFlag(AdministrationRights.EditUsers);
		public bool AdminEditPassword => user.AdministrationRights.Value.HasFlag(AdministrationRights.EditPassword);
		public bool AdminRestartServer => user.AdministrationRights.Value.HasFlag(AdministrationRights.RestartHost);
		public bool AdminChangeVersion => user.AdministrationRights.Value.HasFlag(AdministrationRights.ChangeVersion);

		readonly IUsersClient usersClient;
		readonly PageContextViewModel pageContext;

		User user;
		bool refreshing;
		bool error;

		public UserViewModel(IUsersClient usersClient, User user, PageContextViewModel pageContext)
		{
			this.usersClient = usersClient ?? throw new ArgumentNullException(nameof(usersClient));
			this.user = user ?? throw new ArgumentNullException(nameof(user));
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));

			Close = new EnumCommand<UserCommand>(UserCommand.Close, this);
			Refresh = new EnumCommand<UserCommand>(UserCommand.Refresh, this);
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
					await RunRequest(async () => user = await usersClient.Get(user.Id, cancellationToken).ConfigureAwait(true)).ConfigureAwait(false);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}