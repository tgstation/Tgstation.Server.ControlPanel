using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class AdministrationViewModel : ViewModelBase, ITreeNode, ICommandReceiver<AdministrationViewModel.AdministrationCommand>
	{
		const string ConfirmText = "Confirm?";
		const string InitialRestartText = "Restart Server";
		const string InitialUpdateText = "Update Server";

		public enum AdministrationCommand
		{
			Close,
			Restart,
			Update,
			OpenGitHub,
			Refresh
		}

		public string Title => "Administration";
		
		public bool IsExpanded { get; set; }
		public string Icon
		{
			get => icon;
			set => this.RaiseAndSetIfChanged(ref icon, value);
		}

		public IReadOnlyList<ITreeNode> Children => null;

		public string RestartText { get; set; }
		public string UpdateText { get; set; }

		public string LatestVersionString => model?.LatestVersion?.ToString() ?? "Unknown";
		public string WindowsHostMachine => model == null ? "Unknown" : model.WindowsHost ? "Yes" : "No";
		public string GitHubUrl => model?.TrackedRepositoryUrl.ToString() ?? "Unknown";

		public string NewVersion
		{
			get => newVersion;
			set
			{
				this.RaiseAndSetIfChanged(ref newVersion, value);
				Update.Recheck();
			}
		}

		public bool Error => ErrorMessage != null;

		public string ErrorMessage
		{
			get => errorMessage;
			set
			{
				this.RaiseAndSetIfChanged(ref errorMessage, value);
				this.RaisePropertyChanged(nameof(Error));
			}
		}

		public bool Refreshing
		{
			get => refreshing;
			set
			{
				this.RaiseAndSetIfChanged(ref refreshing, value);
				RefreshCmd.Recheck();
			}
		}

		public ICommand Close { get; }
		public EnumCommand<AdministrationCommand> Restart { get; }
		public EnumCommand<AdministrationCommand> RefreshCmd { get; }
		public EnumCommand<AdministrationCommand> Update { get; }
		public EnumCommand<AdministrationCommand> OpenGitHub { get; }

		readonly PageContextViewModel pageContext;
		readonly IAdministrationClient administrationClient;
		readonly IUserRightsProvider userRightsProvider;
		readonly ConnectionManagerViewModel connectionManagerViewModel;

		Administration model;
		string newVersion;
		string icon;
		string errorMessage;

		bool confirmingRestart;
		bool confirmingUpdate;
		bool refreshing;

		public AdministrationViewModel(PageContextViewModel pageContext, IAdministrationClient administrationClient, IUserRightsProvider userRightsProvider, ConnectionManagerViewModel connectionManagerViewModel)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.administrationClient = administrationClient ?? throw new ArgumentNullException(nameof(administrationClient));
			this.userRightsProvider = userRightsProvider ?? throw new ArgumentNullException(nameof(userRightsProvider));
			this.connectionManagerViewModel = connectionManagerViewModel ?? throw new ArgumentNullException(nameof(connectionManagerViewModel));

			UpdateText = InitialUpdateText;
			RestartText = InitialRestartText;

			Close = new EnumCommand<AdministrationCommand>(AdministrationCommand.Close, this);
			Restart = new EnumCommand<AdministrationCommand>(AdministrationCommand.Restart, this);
			Update = new EnumCommand<AdministrationCommand>(AdministrationCommand.Update, this);
			OpenGitHub = new EnumCommand<AdministrationCommand>(AdministrationCommand.OpenGitHub, this);
			RefreshCmd = new EnumCommand<AdministrationCommand>(AdministrationCommand.Refresh, this);

			userRightsProvider.OnUpdated += (a, b) =>
			{
				Restart.Recheck();
				Update.Recheck();
				RecheckIcon();
			};
			RecheckIcon();

			async void InitialLoad() => await Refresh(default).ConfigureAwait(true);
			InitialLoad();
		}

		async Task Refresh(CancellationToken cancellationToken)
		{
			Refreshing = true;
			Icon = "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png";
			ErrorMessage = null;
			try
			{
				model = await administrationClient.Read(cancellationToken).ConfigureAwait(true);
			}
			catch(ClientException e)
			{
				ErrorMessage = e.Message;
			}
			finally
			{
				using (DelayChangeNotifications())
				{
					Refreshing = false;
					RecheckIcon();
					if (NewVersion == null)
						NewVersion = model.LatestVersion?.ToString();
					this.RaisePropertyChanged(nameof(GitHubUrl));
					this.RaisePropertyChanged(nameof(WindowsHostMachine));
					this.RaisePropertyChanged(nameof(LatestVersionString));
					OpenGitHub.Recheck();
				}
			}
		}

		void RecheckIcon()
		{
			if (!userRightsProvider.AdministrationRights.HasFlag(AdministrationRights.ChangeVersion) && !userRightsProvider.AdministrationRights.HasFlag(AdministrationRights.RestartHost))
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg";
			else
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.gear.png";
		}

		public Task HandleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(AdministrationCommand command)
		{
			switch (command)
			{
				case AdministrationCommand.Close:
					return true;
				case AdministrationCommand.Refresh:
					return !Refreshing;
				case AdministrationCommand.Restart:
					return userRightsProvider.AdministrationRights.HasFlag(AdministrationRights.RestartHost);
				case AdministrationCommand.Update:
					return Version.TryParse(NewVersion, out var success) && userRightsProvider.AdministrationRights.HasFlag(AdministrationRights.ChangeVersion);
				case AdministrationCommand.OpenGitHub:
					return model?.TrackedRepositoryUrl != null;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public async Task RunCommand(AdministrationCommand command, CancellationToken cancellationToken)
		{
			async void UnsetConfirm(bool restart)
			{
				await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
				if (restart)
				{
					confirmingRestart = false;
					RestartText = InitialRestartText;
					this.RaisePropertyChanged(nameof(RestartText));
				}
				else
				{
					confirmingUpdate = false;
					UpdateText = InitialUpdateText;
					this.RaisePropertyChanged(nameof(UpdateText));
				}
			}
			switch (command)
			{
				case AdministrationCommand.Close:
					pageContext.ActiveObject = null;
					break;
				case AdministrationCommand.Restart:
					if (!confirmingRestart)
					{
						confirmingRestart = true;
						RestartText = ConfirmText;
						this.RaisePropertyChanged(nameof(RestartText));
						UnsetConfirm(true);
						break;
					}
					try
					{
						await administrationClient.Restart(cancellationToken).ConfigureAwait(false);
					}
					catch (ClientException)
					{
						return;
					}
					await connectionManagerViewModel.BeginConnect(cancellationToken).ConfigureAwait(true);
					break;
				case AdministrationCommand.Refresh:
					await Refresh(cancellationToken).ConfigureAwait(true);
					break;
				case AdministrationCommand.Update:
					if (!confirmingUpdate)
					{
						confirmingUpdate = true;
						UpdateText = ConfirmText;
						this.RaisePropertyChanged(nameof(UpdateText));
						UnsetConfirm(false);
						break;
					}
					try
					{
						await administrationClient.Update(new Administration
						{
							NewVersion = Version.Parse(NewVersion)
						}, cancellationToken).ConfigureAwait(false);
					}
					catch (ClientException)
					{
						return;
					}
					await connectionManagerViewModel.BeginConnect(cancellationToken).ConfigureAwait(true);
					break;
				case AdministrationCommand.OpenGitHub:
					ControlPanel.LaunchUrl(GitHubUrl);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}
