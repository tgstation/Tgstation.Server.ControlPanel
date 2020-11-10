using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class LogFileViewModel : ViewModelBase, ICommandReceiver<LogFileViewModel.DownloadCommand>
	{
		public enum DownloadCommand
		{
			Download
		}

		public string DisplayText => Working ? "Downloading..." : $"Download {logFile.Name}";

		public bool Working
		{
			get => working;
			set
			{
				this.RaiseAndSetIfChanged(ref working, value);
				this.RaisePropertyChanged(nameof(DisplayText));
			}
		}

		bool working;

		readonly LogFile logFile;
		readonly IUserRightsProvider userRightsProvider;
		readonly IAdministrationClient administrationClient;

		public EnumCommand<DownloadCommand> Download { get; }

		public LogFileViewModel(LogFile logFile, IAdministrationClient administrationClient, IUserRightsProvider userRightsProvider)
		{
			this.logFile = logFile ?? throw new ArgumentNullException(nameof(logFile));
			this.administrationClient = administrationClient ?? throw new ArgumentNullException(nameof(administrationClient));
			this.userRightsProvider = userRightsProvider ?? throw new ArgumentNullException(nameof(userRightsProvider));

			Download = new EnumCommand<DownloadCommand>(DownloadCommand.Download, this);
			userRightsProvider.OnUpdated += (sender, e) =>
			{
				Download.Recheck();
			};
		}

		public bool CanRunCommand(DownloadCommand command) => userRightsProvider.AdministrationRights.HasFlag(AdministrationRights.DownloadLogs);

		public async Task RunCommand(DownloadCommand command, CancellationToken cancellationToken)
		{
			var sfd = new SaveFileDialog
			{
				Title = "Save Log",
				InitialFileName = logFile.Name,
				DefaultExtension = "log"
			};
			Working = true;
			try
			{
				if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime) {
					var savePath = await sfd.ShowAsync(lifetime.MainWindow).ConfigureAwait(true);
					if (Directory.Exists(System.IO.Path.GetDirectoryName(savePath)))
					{
						var fullLog = await administrationClient.GetLog(logFile, cancellationToken);
						await File.WriteAllBytesAsync(savePath, fullLog.Content, cancellationToken);
					}
				}
			}
			catch { }
			finally
			{
				Working = false;
			}
		}
	}
}