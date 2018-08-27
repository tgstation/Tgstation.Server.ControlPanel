using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
			OpenGitHub
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

		public string LatestVersionString => model.LatestVersion.ToString();
		public string WindowsHostMachine => model.WindowsHost ? "Yes" : "No";
		public string GitHubUrl => model.TrackedRepositoryUrl.ToString();

		public string NewVersion
		{
			get => newVersion;
			set
			{
				this.RaiseAndSetIfChanged(ref newVersion, value);
				Update.Recheck();
			}
		}

		public ICommand Close { get; }
		public EnumCommand<AdministrationCommand> Restart { get; }
		public EnumCommand<AdministrationCommand> Update { get; }
		public EnumCommand<AdministrationCommand> OpenGitHub { get; }

		readonly PageContextViewModel pageContext;
		readonly IAdministrationClient administrationClient;
		readonly IUserRightsProvider userRightsProvider;
		readonly ConnectionManagerViewModel connectionManagerViewModel;

		Administration model;
		string newVersion;
		string icon;

		bool confirmingRestart;
		bool confirmingUpdate;

		public AdministrationViewModel(PageContextViewModel pageContext, IAdministrationClient administrationClient, IUserRightsProvider userRightsProvider, ConnectionManagerViewModel connectionManagerViewModel)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.administrationClient = administrationClient ?? throw new ArgumentNullException(nameof(administrationClient));
			this.userRightsProvider = userRightsProvider ?? throw new ArgumentNullException(nameof(userRightsProvider));
			this.connectionManagerViewModel = connectionManagerViewModel ?? throw new ArgumentNullException(nameof(connectionManagerViewModel));

			UpdateText = InitialUpdateText;
			RestartText = InitialRestartText;

			Icon = "resm:Tgstation.Server.ControlPanel.Assets.gear.png";

			Close = new EnumCommand<AdministrationCommand>(AdministrationCommand.Close, this);
			Restart = new EnumCommand<AdministrationCommand>(AdministrationCommand.Restart, this);
			Update = new EnumCommand<AdministrationCommand>(AdministrationCommand.Update, this);
			OpenGitHub = new EnumCommand<AdministrationCommand>(AdministrationCommand.OpenGitHub, this);

			userRightsProvider.OnUpdated += (a, b) =>
			{
				Restart.Recheck();
				Update.Recheck();
			};
		}

		async Task Refresh(CancellationToken cancellationToken)
		{
			model = await administrationClient.Read(cancellationToken).ConfigureAwait(true);
			using (DelayChangeNotifications())
			{
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.gear.png";
				if (NewVersion == null)
					NewVersion = model.LatestVersion?.ToString();
				this.RaisePropertyChanged(nameof(GitHubUrl));
				this.RaisePropertyChanged(nameof(WindowsHostMachine));
				this.RaisePropertyChanged(nameof(LatestVersionString));
				OpenGitHub.Recheck();
			}
		}

		public async Task HandleDoubleClick(CancellationToken cancellationToken)
		{
			if (!userRightsProvider.AdministrationRights.HasFlag(AdministrationRights.ChangeVersion) && !userRightsProvider.AdministrationRights.HasFlag(AdministrationRights.RestartHost))
			{
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg";
				return;
			}
			Icon = "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png";
			pageContext.ActiveObject = this;
			await Refresh(cancellationToken).ConfigureAwait(true);
		}

		public bool CanRunCommand(AdministrationCommand command)
		{
			switch (command)
			{
				case AdministrationCommand.Close:
					return true;
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
					try
					{
						Process.Start(new ProcessStartInfo
						{
							FileName = GitHubUrl,
							UseShellExecute = true
						}).Dispose();
					}
					catch
					{
						try
						{
							Process.Start("xdg-open", GitHubUrl).Dispose();
						}
						catch { }
					}
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}
