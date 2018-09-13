using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Tgstation.Server.Api;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Client;
using Tgstation.Server.ControlPanel.Models;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	public class MainWindowViewModel : ViewModelBase, IDisposable, ICommandReceiver<MainWindowViewModel.MainWindowCommand>, IRequestLogger
	{
		public enum MainWindowCommand
		{
			NewServerConnection,
			CopyConsole,
			AppUpdate,
			ReportIssue
		}

		readonly ProductHeaderValue productHeaderValue;

		public static string Versions => String.Format(CultureInfo.InvariantCulture, "Version: {0}, API Version: {1}", Assembly.GetExecutingAssembly().GetName().Version, ApiHeaders.Version);

		public static string Meme
		{
			get
			{
				var memes = new string[]
				{
					"Proudly created by Cyberboss",
					"True Canadian Beer",
					"Brainlet Resistant",
					"Need Milk",
					"Absolute Seperation",
					"Deleting Data Directory...",
					"When You Code It",
					"Contains Technical Debt"
				};
				return memes[new Random().Next(memes.Length)];
			}
		}

		public string ConsoleContent
		{
			get => consoleContent;
			set => this.RaiseAndSetIfChanged(ref consoleContent, value);
		}

		public ICommand AddServerCommand { get; }

		public ICommand CopyConsole { get; }

		public EnumCommand<MainWindowCommand> AppUpdate { get; }

		public ICommand ReportIssue { get; }

		public static MainWindowViewModel Singleton { get; private set; }

		public PageContextViewModel PageContext { get; }

		public bool CanUpdate => updater.Functional && !noUpdatesAvailable;

		public JobManagerViewModel Jobs { get; }

		public List<ConnectionManagerViewModel> Connections
		{
			get => connections;
			private set => this.RaiseAndSetIfChanged(ref connections, value);
		}

		public int UpdateProgress
		{
			get => updateProgress;
			set
			{
				this.RaiseAndSetIfChanged(ref updateProgress, value);
				this.RaisePropertyChanged(nameof(ShowUpdateProgress));
			}
		}

		public string GitHubToken
		{
			get => settings.GitHubToken.Password;
			set
			{
				settings.GitHubToken.Password = value;
				UpdateGitHubClient();
			}
		}

		public string UpdateText
		{
			get => updateText;
			set => this.RaiseAndSetIfChanged(ref updateText, value);
		}

		public bool ShowUpdateProgress => UpdateProgress != -1;

		readonly IUpdater updater;
		readonly IServerClientFactory serverClientFactory;
		readonly IUrlEncoder urlEncoder;
		readonly CancellationTokenSource settingsSaveLoopCts;
		readonly Task settingsSaveLoop;
		readonly UserSettings settings;

		readonly string storageDirectory;
		readonly string settingsPath;

		List<ConnectionManagerViewModel> connections;

		Octokit.IGitHubClient gitHubClient;

		string consoleContent;
		string updateText;

		int updateProgress;
		bool updateReady;
		bool updateInstalled;
		bool noUpdatesAvailable;

		public MainWindowViewModel(IUrlEncoder urlEncoder, IUpdater updater)
		{
			Singleton = this;
			this.urlEncoder = urlEncoder ?? throw new ArgumentNullException(nameof(urlEncoder));
			this.updater = updater ?? throw new ArgumentNullException(nameof(updater));

			var assemblyName = Assembly.GetExecutingAssembly().GetName();
			productHeaderValue = new ProductHeaderValue(assemblyName.Name, assemblyName.Version.ToString());

			serverClientFactory = new ServerClientFactory(productHeaderValue);

			var storagePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			const string SettingsFileName = "settings.json";
			storageDirectory = Path.Combine(storagePath, productHeaderValue.Name);
			settingsPath = Path.Combine(storageDirectory, SettingsFileName);
			settings = LoadSettings();

			UpdateGitHubClient();

			settingsSaveLoopCts = new CancellationTokenSource();
			settingsSaveLoop = SettingsSaveLoop(settingsSaveLoopCts.Token);

			ConsoleContent = "Request details will be shown here...";

			PageContext = new PageContextViewModel();
			Jobs = new JobManagerViewModel();

			AddServerCommand = new EnumCommand<MainWindowCommand>(MainWindowCommand.NewServerConnection, this);
			CopyConsole = new EnumCommand<MainWindowCommand>(MainWindowCommand.CopyConsole, this);
			AppUpdate = new EnumCommand<MainWindowCommand>(MainWindowCommand.AppUpdate, this);
			ReportIssue = new EnumCommand<MainWindowCommand>(MainWindowCommand.ReportIssue, this);
		}

		void UpdateGitHubClient()
		{
			var tmpClient = new Octokit.GitHubClient(new Octokit.ProductHeaderValue(productHeaderValue.Name, productHeaderValue.Version));
			if (!String.IsNullOrEmpty(GitHubToken))
				tmpClient.Credentials = new Octokit.Credentials(GitHubToken);
			gitHubClient = tmpClient;
		}

		ConnectionManagerViewModel CreateConnection(Connection connection)
		{
			ConnectionManagerViewModel newManager = null;
			newManager = new ConnectionManagerViewModel(serverClientFactory, this, connection, PageContext, () =>
			{
				settings.Connections.Remove(connection);
				Connections = new List<ConnectionManagerViewModel>(Connections.Where(x => x != newManager));
			}, Jobs, gitHubClient);
			return newManager;
		}

		UserSettings LoadSettings()
		{
			UserSettings settings = null;
			try
			{
				var settingsFile = File.ReadAllText(settingsPath);
				settings = JsonConvert.DeserializeObject<UserSettings>(settingsFile);
			}
			catch (IOException) { }
			catch (JsonException) { }

			if (settings == null)
				settings = new UserSettings();
			settings.Connections = settings.Connections ?? new List<Connection>();
			settings.GitHubToken = settings.GitHubToken ?? new Credentials()
			{
				Password = String.Empty
			};
			settings.GitHubToken.AllowSavingPassword = true;
			return settings;
		}

		void SaveSettings()
		{
			try
			{
				Directory.CreateDirectory(storageDirectory);
				var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
				File.WriteAllText(settingsPath, json);
			}
			catch (IOException) { }
		}

		async Task SettingsSaveLoop(CancellationToken cancellationToken)
		{
			try
			{
				while (true)
				{
					await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
					cancellationToken.ThrowIfCancellationRequested();
					SaveSettings();
				}
			}
			catch (OperationCanceledException) { }
		}

		public void Dispose()
		{
			settingsSaveLoopCts.Cancel();
			settingsSaveLoop.GetAwaiter().GetResult();
			SaveSettings();
			updater.Dispose();
		}

		public async Task LogRequest(HttpRequestMessage requestMessage, CancellationToken cancellationToken)
		{
			var instancePart = String.Empty;
			if (requestMessage.Headers.TryGetValues("Instance", out var values))
				instancePart = String.Format(CultureInfo.InvariantCulture, " I:{0}", values.First());
			var bodyPart = String.Empty;
			var bodyString = await (requestMessage.Content?.ReadAsStringAsync() ?? Task.FromResult<string>(null)).ConfigureAwait(false);
			if (!String.IsNullOrEmpty(bodyString))
			{
				//may contain the password for User models, censor it
				try
				{
					var userModel = JsonConvert.DeserializeObject<UserUpdate>(bodyString);
					if (userModel.Password != null)
					{
						userModel.Password = String.Join("", Enumerable.Repeat('*', userModel.Password.Length));
						bodyString = JsonConvert.SerializeObject(userModel);
					}
				}
				catch (JsonException)
				{
					try
					{
						var repoModel = JsonConvert.DeserializeObject<Repository>(bodyString);
						if (repoModel.AccessToken != null)
						{
							repoModel.AccessToken = String.Join("", Enumerable.Repeat('*', repoModel.AccessToken.Length));
							bodyString = JsonConvert.SerializeObject(repoModel);
						}
					}
					catch (JsonException) { }
				}
				bodyPart = String.Format(CultureInfo.InvariantCulture, " => {0}", bodyString);
			}
			lock (this)
				ConsoleContent = String.Format(CultureInfo.InvariantCulture, "{0}{1}[{2}]: {3} {4}{5}{6}", ConsoleContent, Environment.NewLine, DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture), requestMessage.Method, requestMessage.RequestUri, instancePart, bodyPart);
		}

		public async Task LogResponse(HttpResponseMessage responseMessage, CancellationToken cancellationToken)
		{
			var requestMessage = responseMessage.RequestMessage;
			var instancePart = String.Empty;
			if (requestMessage.Headers.TryGetValues("Instance", out var values))
				instancePart = String.Format(CultureInfo.InvariantCulture, " I:{0}", values.First());
			var bodyPart = String.Empty;
			var bodyString = await (responseMessage.Content?.ReadAsStringAsync() ?? Task.FromResult<string>(null)).ConfigureAwait(false);
			if (!String.IsNullOrEmpty(bodyString))
				bodyPart = String.Format(CultureInfo.InvariantCulture, " => {0}", bodyString);
			lock (this)
				ConsoleContent = String.Format(CultureInfo.InvariantCulture, "{0}{1}[{2}]: HTTP {7}: {3} {4}{5}{6}", ConsoleContent, Environment.NewLine, DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture), requestMessage.Method, requestMessage.RequestUri, instancePart, bodyPart, responseMessage.StatusCode);
		}

		public bool CanRunCommand(MainWindowCommand command)
		{
			switch (command)
			{
				case MainWindowCommand.NewServerConnection:
				case MainWindowCommand.CopyConsole:
				case MainWindowCommand.ReportIssue:
					return true;
				case MainWindowCommand.AppUpdate:
					return updater.Functional && updateReady;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public async Task RunCommand(MainWindowCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case MainWindowCommand.NewServerConnection:
					var newConnection = new Connection
					{
						Credentials = new Credentials
						{
							Password = String.Empty
						},
						Username = String.Empty,
						Timeout = TimeSpan.FromSeconds(15),
						JobRequeryRate = TimeSpan.FromSeconds(10),
						Url = new Uri("https://localhost:5000")
					};

					settings.Connections.Add(newConnection);
					using (DelayChangeNotifications())
					{
						var newCm = CreateConnection(newConnection);
						PageContext.ActiveObject = newCm;
						var newConnections = new List<ConnectionManagerViewModel>(Connections)
						{
							newCm
						};
						Connections = newConnections;
						await newCm.OnLoadConnect(cancellationToken).ConfigureAwait(true);
					}
					break;
				case MainWindowCommand.CopyConsole:
					List<string> tmp;
					lock(this)
						tmp = new List<string>(ConsoleContent.Split('\n'));
					tmp.RemoveAt(0);    //remove info line
					var clipboard = String.Join(" ", tmp);
					TextCopy.Clipboard.SetText(clipboard);
					break;
				case MainWindowCommand.AppUpdate:
					if (!updateInstalled)
					{
						UpdateProgress = 0;
						UpdateText = "Installing Update: ";
						await updater.ApplyUpdate(progress => UpdateProgress = progress);
						updateInstalled = true;
						updateReady = true;
						UpdateText = "Update Ready!";
						AppUpdate.Recheck();
					}
					else
						updater.RestartApp();
					break;
				case MainWindowCommand.ReportIssue:
					var body = String.Format("<Please describe your issue here>{0}{0}{0}Console:{0}```{0}{1}{0}```{0}Reported from version {2}", Environment.NewLine, ConsoleContent, Assembly.GetExecutingAssembly().GetName().Version);
					ControlPanel.LaunchUrl(String.Format(CultureInfo.InvariantCulture, "https://github.com/tgstation/Tgstation.Server.ControlPanel/issues/new?body={0}", urlEncoder.UrlEncode(body)));
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public async void AsyncStart()
		{
			var updatesTask = CheckForUpdates();
			Connections = new List<ConnectionManagerViewModel>(settings.Connections.Select(x => CreateConnection(x)));
			await Task.WhenAll(Connections.Select(x => x.OnLoadConnect(default))).ConfigureAwait(true);
			await updatesTask.ConfigureAwait(true);
		}

		async Task CheckForUpdates()
		{
			if (!updater.Functional)
				return;

			UpdateText = "Checking for updates: ";
			UpdateProgress = 0;
			Version newVersion;
			try
			{
				newVersion = await updater.LatestVersion(progress => UpdateProgress = progress).ConfigureAwait(true);
			}
			catch (Exception e)
			{
				UpdateText = "Error fetching update information: " + e.Message;
				return;
			}
			finally
			{
				UpdateProgress = 0;
			}

			if (newVersion == null || newVersion > Assembly.GetExecutingAssembly().GetName().Version)
			{
				UpdateText = "Update Available!";
				updateReady = true;
				AppUpdate.Recheck();
			}
			else
			{
				noUpdatesAvailable = true;
				this.RaisePropertyChanged(nameof(CanUpdate));
			}
		}
	}
}
