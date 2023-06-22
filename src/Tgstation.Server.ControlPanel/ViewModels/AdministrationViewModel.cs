using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;


using Avalonia.Controls;

using ReactiveUI;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Models.Request;
using Tgstation.Server.Api.Models.Response;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client;
using Avalonia;
using Avalonia.Controls.Shapes;
using System.IO;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class AdministrationViewModel : ViewModelBase, ITreeNode, ICommandReceiver<AdministrationViewModel.AdministrationCommand>
	{
		const string ConfirmText = "Confirm?";
		const string InitialRestartText = "Restart Server";
		const string DoUpdateText = "Update Server";
		const string InitialUpdateText = "Read Release Notes";

		public enum AdministrationCommand
		{
			Close,
			Restart,
			Update,
			OpenGitHub,
			Refresh,
			Upload,
		}

		public string Title => model?.LatestVersion > tgsVersion && userRightsProvider.AdministrationRights.HasFlag(AdministrationRights.ChangeVersion) ? "Administration (Update Available)" : "Administration";

		public bool IsExpanded { get; set; }
		public string Icon
		{
			get => icon;
			set => this.RaiseAndSetIfChanged(ref icon, value);
		}

		public IReadOnlyList<ITreeNode> Children => null;

		public string RestartText { get; set; }
		public string UpdateText { get; set; }

		public string UploadText { get; set; }

		public string LatestVersionString => model?.LatestVersion?.ToString() ?? "Unknown";
		public string GitHubUrl => model?.TrackedRepositoryUrl.ToString() ?? "Unknown";

		public string NewVersion
		{
			get => newVersion;
			set
			{
				this.RaiseAndSetIfChanged(ref newVersion, value);
				Update.Recheck();
				Upload.Recheck();
				this.uploadClickIndex = 0;
				this.UploadText = uploadPreamble[0];
				this.UpdateText = InitialUpdateText;
				this.RaisePropertyChanged(nameof(UploadText));
				this.RaisePropertyChanged(nameof(UpdateText));
			}
		}

		public bool Error => ErrorMessage != null;
		public bool CanGetLogs => userRightsProvider.AdministrationRights.HasFlag(AdministrationRights.DownloadLogs);

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
		public EnumCommand<AdministrationCommand> Upload { get; }
		public EnumCommand<AdministrationCommand> OpenGitHub { get; }

		public List<LogFileViewModel> LogFiles
		{
			get => logFiles;
			set => this.RaiseAndSetIfChanged(ref logFiles, value);
		}

		readonly PageContextViewModel pageContext;
		readonly IAdministrationClient administrationClient;
		readonly IUserRightsProvider userRightsProvider;
		readonly ConnectionManagerViewModel connectionManagerViewModel;

		readonly Version tgsVersion;
		AdministrationResponse model;
		string newVersion;
		string icon;
		string errorMessage;

		List<LogFileViewModel> logFiles;
		bool confirmingRestart;
		bool confirmingUpdate;
		string readReleaseNotes;
		bool refreshing;

		static readonly string[] uploadPreamble = new[]
		{
			"Upload Update Package",
			"Do you understand what you're doing?",
			"Did you get a certified update package?",
			"An invalid zip can brick your server",
			"The version set above must be correct",
			"An incorrect version can brick your server",
			"Final Confirmation",
		};

		uint uploadClickIndex;

		public AdministrationViewModel(PageContextViewModel pageContext, IAdministrationClient administrationClient, IUserRightsProvider userRightsProvider, ConnectionManagerViewModel connectionManagerViewModel, Version tgsVersion)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.administrationClient = administrationClient ?? throw new ArgumentNullException(nameof(administrationClient));
			this.userRightsProvider = userRightsProvider ?? throw new ArgumentNullException(nameof(userRightsProvider));
			this.connectionManagerViewModel = connectionManagerViewModel ?? throw new ArgumentNullException(nameof(connectionManagerViewModel));
			this.tgsVersion = tgsVersion ?? throw new ArgumentNullException(nameof(tgsVersion));

			logFiles = new List<LogFileViewModel>();

			UpdateText = InitialUpdateText;
			RestartText = InitialRestartText;
			UploadText = uploadPreamble[uploadClickIndex];

			Close = new EnumCommand<AdministrationCommand>(AdministrationCommand.Close, this);
			Restart = new EnumCommand<AdministrationCommand>(AdministrationCommand.Restart, this);
			Update = new EnumCommand<AdministrationCommand>(AdministrationCommand.Update, this);
			OpenGitHub = new EnumCommand<AdministrationCommand>(AdministrationCommand.OpenGitHub, this);
			RefreshCmd = new EnumCommand<AdministrationCommand>(AdministrationCommand.Refresh, this);
			Upload = new EnumCommand<AdministrationCommand>(AdministrationCommand.Upload, this);

			userRightsProvider.OnUpdated += (a, b) =>
			{
				Restart.Recheck();
				Update.Recheck();
				Upload.Recheck();
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
				var modelTask = administrationClient.Read(cancellationToken);
				if (CanGetLogs)
				{
					LogFiles = (await administrationClient.ListLogs(null, cancellationToken)).Select(x => new LogFileViewModel(x, administrationClient, userRightsProvider)).ToList();
				}

				model = await modelTask.ConfigureAwait(true);
			}
			catch (ClientException e)
			{
				ErrorMessage = e.Message;
			}
			finally
			{
				using (DelayChangeNotifications())
				{
					Refreshing = false;
					uploadClickIndex = 0;
					UploadText = uploadPreamble[uploadClickIndex];
					RecheckIcon();
					NewVersion ??= model?.LatestVersion?.ToString();
					this.RaisePropertyChanged(nameof(GitHubUrl));
					this.RaisePropertyChanged(nameof(LatestVersionString));
					this.RaisePropertyChanged(nameof(Title));
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
			return command switch
			{
				AdministrationCommand.Close => true,
				AdministrationCommand.Refresh => !Refreshing,
				AdministrationCommand.Restart => userRightsProvider.AdministrationRights.HasFlag(AdministrationRights.RestartHost),
				AdministrationCommand.Upload => Version.TryParse(NewVersion, out var _) && userRightsProvider.AdministrationRights.HasFlag(AdministrationRights.UploadVersion),
				AdministrationCommand.Update => Version.TryParse(NewVersion, out var _) && userRightsProvider.AdministrationRights.HasFlag(AdministrationRights.ChangeVersion),
				AdministrationCommand.OpenGitHub => model?.TrackedRepositoryUrl != null,
				_ => throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!"),
			};
		}

		public async Task RunCommand(AdministrationCommand command, CancellationToken cancellationToken)
		{
			async void UnsetConfirm(bool restart, uint? upload)
			{
				await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
				if (restart)
				{
					confirmingRestart = false;
					RestartText = InitialRestartText;
					this.RaisePropertyChanged(nameof(RestartText));
				}
				else if (upload.HasValue)
				{
					if (uploadClickIndex == upload)
					{
						uploadClickIndex = 0;
						UploadText = uploadPreamble[0];
						this.RaisePropertyChanged(nameof(UploadText));
					}
				}
				else
				{
					confirmingUpdate = false;
					UpdateText = DoUpdateText;
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
						UnsetConfirm(true, null);
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
					await connectionManagerViewModel.BeginConnect(null, cancellationToken).ConfigureAwait(true);
					break;
				case AdministrationCommand.Upload:
					if(++uploadClickIndex == uploadPreamble.Length)
					{
						uploadClickIndex = 0;
						UploadText = uploadPreamble[uploadClickIndex];

						this.RaisePropertyChanged(nameof(UploadText));

						var versionUsed = Version.Parse(NewVersion);

						if (!(Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime2))
							return;

						var ofd = new OpenFileDialog
						{
							Title = $"Select TGS Update Package for v{versionUsed}",
							InitialFileName = "ServerUpdatePackage.zip",
							AllowMultiple = false,
							Filters = new List<FileDialogFilter>
							{
								new FileDialogFilter
								{
									Extensions = new List<string>
									{
										"*.zip"
									},
									Name = $"TGS Update Package v{versionUsed}",
								}
							},
						};

						var uploadPath = (await ofd.ShowAsync(lifetime2.MainWindow).ConfigureAwait(true))[0];
						await using var zipStream = new FileStream(
							uploadPath,
							FileMode.Open,
							FileAccess.Read,
							FileShare.Read | FileShare.Delete,
							4096,
							FileOptions.Asynchronous | FileOptions.SequentialScan);
						try
						{
							await administrationClient.Update(new ServerUpdateRequest
							{
								NewVersion = Version.Parse(NewVersion),
								UploadZip = true,
							}, 
							zipStream,
							cancellationToken).ConfigureAwait(false);
						}
						catch (ClientException)
						{
							return;
						}

						await connectionManagerViewModel.BeginConnect(null, cancellationToken).ConfigureAwait(true);
					}
					else
					{
						UploadText = uploadPreamble[uploadClickIndex];
						this.RaisePropertyChanged(nameof(UploadText));
						UnsetConfirm(false, uploadClickIndex);
					}

					break;
				case AdministrationCommand.Refresh:
					await Refresh(cancellationToken).ConfigureAwait(true);
					break;
				case AdministrationCommand.Update:
					if (readReleaseNotes != NewVersion)
					{
						readReleaseNotes = NewVersion;
						UpdateText = DoUpdateText;
						this.RaisePropertyChanged(nameof(UpdateText));
						ControlPanel.LaunchUrl(model.TrackedRepositoryUrl + "/releases/tag/tgstation-server-v" + NewVersion);
						break;
					}
					else if (!confirmingUpdate)
					{
						confirmingUpdate = true;
						UpdateText = ConfirmText;
						this.RaisePropertyChanged(nameof(UpdateText));
						UnsetConfirm(false, null);
						break;
					}
					try
					{
						await administrationClient.Update(new ServerUpdateRequest
						{
							NewVersion = Version.Parse(NewVersion)
						}, null, cancellationToken).ConfigureAwait(false);
					}
					catch (ClientException)
					{
						return;
					}
					await connectionManagerViewModel.BeginConnect(null, cancellationToken).ConfigureAwait(true);
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
