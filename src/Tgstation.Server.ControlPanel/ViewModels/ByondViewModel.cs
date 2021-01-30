using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class ByondViewModel : ViewModelBase, ITreeNode, ICommandReceiver<ByondViewModel.ByondCommand>
	{
		public enum ByondCommand
		{
			Update,
			Refresh,
			Close,
			Browse
		}
		public string Title => "Byond";

		public string Icon
		{
			get => icon;
			set => this.RaiseAndSetIfChanged(ref icon, value);
		}

		public string CurrentVersion => data != null ? FormatByondVersion(data) : "Unknown";

		public string ApplyText => string.IsNullOrWhiteSpace(ByondZipPath) ?
			InstalledVersions
				.Select(x => Version.Parse(x))
				.Any(x => x.Major == NewMajor && x.Minor == NewMinor)
				? "Set Active Version"
				: "Install Version"
			: "Upload Custom Version";

		public IReadOnlyList<string> InstalledVersions
		{
			get => installedVersions;
			set
			{
				this.RaiseAndSetIfChanged(ref installedVersions, value);
				this.RaisePropertyChanged(nameof(HasInstalledVersions));
				this.RaisePropertyChanged(nameof(ApplyText));
			}
		}

		public string ByondZipPath
		{
			get => customZipPath;
			set
			{
				customZipPath = value;
				this.RaisePropertyChanged(nameof(ApplyText));
				Update.Recheck();
			}
		}

		public bool HasInstalledVersions => InstalledVersions != null;

		public bool IsExpanded { get; set; }

		public bool Refreshing
		{
			get => refreshing;
			set => this.RaiseAndSetIfChanged(ref refreshing, value);
		}

		public bool CanRead => rightsProvider.ByondRights.HasFlag(ByondRights.ReadActive);
		public bool CanList => rightsProvider.ByondRights.HasFlag(ByondRights.ListInstalled);
		public bool CanInstall => rightsProvider.ByondRights.HasFlag(ByondRights.InstallOfficialOrChangeActiveVersion);
		public bool CanUpload => rightsProvider.ByondRights.HasFlag(ByondRights.InstallCustomVersion);

		public int NewMajor
		{
			get => newMajor;
			set
			{
				this.RaiseAndSetIfChanged(ref newMajor, value);
				this.RaisePropertyChanged(nameof(ApplyText));
				Update.Recheck();
			}
		}

		public int NewMinor
		{
			get => newMinor;
			set
			{
				this.RaiseAndSetIfChanged(ref newMinor, value);
				this.RaisePropertyChanged(nameof(ApplyText));
				Update.Recheck();
			}
		}
		public int Prebuild
		{
			get => prebuild;
			set
			{
				this.RaiseAndSetIfChanged(ref prebuild, value);
				this.RaisePropertyChanged(nameof(ApplyText));
				Update.Recheck();
			}
		}

		public IReadOnlyList<ITreeNode> Children => null;

		public EnumCommand<ByondCommand> Refresh { get; }
		public EnumCommand<ByondCommand> Update { get; }
		public EnumCommand<ByondCommand> Close { get; }
		public EnumCommand<ByondCommand> Browse { get; }

		readonly PageContextViewModel pageContext;
		readonly IByondClient byondClient;
		readonly IInstanceJobSink jobSink;
		readonly IInstanceUserRightsProvider rightsProvider;

		int newMajor;
		int newMinor;
		int prebuild;

		IReadOnlyList<string> installedVersions;
		Byond data;
		string icon;
		string customZipPath;
		bool refreshing;

		public ByondViewModel(PageContextViewModel pageContext, IByondClient byondClient, IInstanceJobSink jobSink, IInstanceUserRightsProvider rightsProvider)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.byondClient = byondClient ?? throw new ArgumentNullException(nameof(byondClient));
			this.jobSink = jobSink ?? throw new ArgumentNullException(nameof(jobSink));
			this.rightsProvider = rightsProvider ?? throw new ArgumentNullException(nameof(rightsProvider));

			Refresh = new EnumCommand<ByondCommand>(ByondCommand.Refresh, this);
			Update = new EnumCommand<ByondCommand>(ByondCommand.Update, this);
			Browse = new EnumCommand<ByondCommand>(ByondCommand.Browse, this);

			rightsProvider.OnUpdated += (a, b) =>
			{
				this.RaisePropertyChanged(nameof(CanRead));
				this.RaisePropertyChanged(nameof(CanInstall));
				this.RaisePropertyChanged(nameof(CanList));
				this.RaisePropertyChanged(nameof(CanUpload));

				Refresh.Recheck();
				Update.Recheck();
				Browse.Recheck();
			};

			NewMajor = 513;
			NewMinor = 1527;

			async void InitialLoad()
			{
				try
				{
					using var tempClient = new HttpClient();
					var latestVersionTask = tempClient.GetAsync("https://secure.byond.com/download/version.txt");
					await Load(default).ConfigureAwait(false);
					var latestVersionResponse = await latestVersionTask;
					var latestVersionText = await latestVersionResponse.Content.ReadAsStringAsync();
					if (Version.TryParse(latestVersionText, out var latestVersion))
					{
						NewMajor = latestVersion.Major;
						NewMinor = latestVersion.Minor;
					}
				}
				catch (Exception ex)
				{
					MainWindowViewModel.HandleException(ex);
				}
			}
			InitialLoad();
		}

		static string FormatByondVersion(Byond byond) => byond.Version == null ? "None" :
			byond.Version.Build > 0
				? string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", byond.Version.Major, byond.Version.Minor, byond.Version.Build)
				: string.Format(CultureInfo.InvariantCulture, "{0}.{1}", byond.Version.Major, byond.Version.Minor);

		async Task Load(CancellationToken cancellationToken)
		{
			if (!CanRead && !CanList)
			{
				Icon = CanInstall ? "resm:Tgstation.Server.ControlPanel.Assets.byond.jpg" : "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg";
				return;
			}

			Icon = "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png";
			Refreshing = true;
			Refresh.Recheck();
			Update.Recheck();
			try
			{
				var dataTask = byondClient.ActiveVersion(cancellationToken);
				var installTask = byondClient.InstalledVersions(null, cancellationToken);
				data = await dataTask.ConfigureAwait(true);
				this.RaisePropertyChanged(nameof(CurrentVersion));
				InstalledVersions = (await installTask.ConfigureAwait(true)).Select(x => FormatByondVersion(x)).ToList();
				Prebuild = 0;
			}
			finally
			{
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.byond.jpg";
				Refreshing = false;
				Refresh.Recheck();
				Update.Recheck();
			}
		}
		public Task HandleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		bool CheckZipExists()
		{
			try
			{
				return File.Exists(ByondZipPath);
			}
			catch
			{
				return false;
			}
		}

		public bool CanRunCommand(ByondCommand command)
		{
			return command switch
			{
				ByondCommand.Refresh => !Refreshing && (CanRead || CanList),
				ByondCommand.Update => !Refreshing
				&& ((CanInstall && string.IsNullOrWhiteSpace(ByondZipPath)
				&& (Prebuild == 0 || InstalledVersions
					.Select(x => Version.Parse(x))
					.Any(x => x == new Version(NewMajor, NewMinor, Prebuild))))
					|| CheckZipExists()),
				ByondCommand.Close => true,
				ByondCommand.Browse => !Refreshing && CanUpload,
				_ => throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!"),
			};
		}

		public async Task RunCommand(ByondCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case ByondCommand.Refresh:
					await Load(cancellationToken).ConfigureAwait(true);
					break;
				case ByondCommand.Update:
					Refreshing = true;
					Refresh.Recheck();
					Update.Recheck();
					try
					{
						Stream zipStream = null;
						if (!string.IsNullOrWhiteSpace(ByondZipPath))
							zipStream = new FileStream(ByondZipPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 8192, true);
						using (zipStream)
						{
							data = await byondClient.SetActiveVersion(
								new Byond
								{
									Version = Prebuild == 0 ? new Version(NewMajor, NewMinor) : new Version(NewMajor, NewMinor, Prebuild),
									UploadCustomZip = zipStream != null,
								},
								zipStream,
								cancellationToken)
								.ConfigureAwait(true);
						}
						if (data.InstallJob != null)
							jobSink.RegisterJob(data.InstallJob, Load);
						this.RaisePropertyChanged(CurrentVersion);
					}
					finally
					{
						Refreshing = false;
						Refresh.Recheck();
						Update.Recheck();
					}
					break;
				case ByondCommand.Close:
					pageContext.ActiveObject = null;
					break;
				case ByondCommand.Browse:
					var ofd = new OpenFileDialog
					{
						Title = "Upload Custom BYOND Version",
						AllowMultiple = false,
						InitialFileName = Path.GetFileName(ByondZipPath),
						Filters = new List<FileDialogFilter>
						{
							new FileDialogFilter
							{
								Name = "Zip Files",
								Extensions = new List<string>
								{
									"zip"
								}
							}
						}
					};
					if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
					{
						ByondZipPath = (await ofd.ShowAsync(lifetime.MainWindow).ConfigureAwait(true))[0] ?? ByondZipPath;
					}
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}
