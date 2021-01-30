using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Octokit.Internal;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
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

		public static string Versions => string.Format(CultureInfo.InvariantCulture, "Version: {0}, API Version: {1}", Assembly.GetExecutingAssembly().GetName().Version, ApiHeaders.Version);

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

		public static void HandleException(Exception ex)
		{
			Singleton.AddToConsole($"UNCAUGHT EXCEPTION! Exception: {ex}");
		}

		public MainWindowViewModel(IUpdater updater)
		{
			Singleton = this;
			this.updater = updater ?? throw new ArgumentNullException(nameof(updater));

			var assemblyName = Assembly.GetExecutingAssembly().GetName();
			productHeaderValue = new ProductHeaderValue(assemblyName.Name, assemblyName.Version.ToString());

			serverClientFactory = new ServerClientFactory(productHeaderValue);

			var storagePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			const string SettingsFileName = "settings.json";
			storageDirectory = Path.Combine(storagePath, productHeaderValue.Name);
			AppDomain.CurrentDomain.UnhandledException += (a, b) => GlobalExceptionHandler((Exception)b.ExceptionObject);

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

		void GlobalExceptionHandler(Exception e)
		{
			var exceptionFileName = Path.Combine(storageDirectory, string.Format(CultureInfo.InvariantCulture, "crash_please_report.{0}.log", DateTimeOffset.Now.ToUnixTimeSeconds()));
			File.WriteAllText(exceptionFileName, e.ToString());
			ControlPanel.OpenFolder(storageDirectory);
		}
		void UpdateGitHubClient()
		{
			var tmpClient = new Octokit.GitHubClient(
				new Octokit.Connection(
					new Octokit.ProductHeaderValue(productHeaderValue.Name, productHeaderValue.Version),
					new HttpClientAdapter(() => new LoggingHandler(this))));
			if (!string.IsNullOrEmpty(GitHubToken))
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
			settings.Connections ??= new List<Connection>();
			settings.GitHubToken ??= new Credentials()
			{
				Password = string.Empty
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

		static void CensorBodyString<TBodyType>(Expression<Func<TBodyType, object>> censorExpression, ref string bodyString)
			where TBodyType : new()
		{
			var serializerSettings = new JsonSerializerSettings
			{
				ContractResolver = new CamelCasePropertyNamesContractResolver(),
				Converters = new[] { new VersionConverter() }
			};

			var memberSelectorExpression = (MemberExpression)censorExpression.Body;
			var property = (PropertyInfo)memberSelectorExpression.Member;

			if (property.PropertyType != typeof(string)
				&& property.PropertyType != typeof(byte[]))
				throw new InvalidOperationException("Must be string or byte array!");

			var workingBodyString = bodyString;
			bool TryCensorProperty(object model)
			{
				var censorField = property.GetValue(model);
				if (censorField == null)
					return false;

				if (property.PropertyType == typeof(string))
					property.SetValue(
						model,
                        string.Join(
                            string.Empty,
							Enumerable.Repeat(
								'*',
								((string)censorField).Length)));
				else
					workingBodyString = workingBodyString.Replace(
						$"\"{Convert.ToBase64String((byte[])censorField)}\"",
						"\"***\"");

				return true;
			}

			try
			{
				var model = JsonConvert.DeserializeObject<TBodyType>(workingBodyString, serializerSettings);
				if (TryCensorProperty(model))
					workingBodyString = JsonConvert.SerializeObject(model, serializerSettings);
			}
			catch (JsonException)
			{
				try
				{
					var models = JsonConvert.DeserializeObject<List<TBodyType>>(workingBodyString, serializerSettings);
					if (models.Any(x => TryCensorProperty(x)))
						workingBodyString = JsonConvert.SerializeObject(models, serializerSettings);
				}
				catch (JsonException) { }
			}

			bodyString = workingBodyString;
		}

		public async Task LogRequest(HttpRequestMessage requestMessage, CancellationToken cancellationToken)
		{
			var instancePart = string.Empty;
			if (requestMessage.Headers.TryGetValues("Instance", out var values))
				instancePart = string.Format(CultureInfo.InvariantCulture, " I:{0}", values.First());
			var bodyPart = string.Empty;

			if (requestMessage.Content is StreamContent)
			{
				AddToConsole($"{requestMessage.Method} {requestMessage.RequestUri}{instancePart} => <STREAM>");
				return;
			}

			var bodyString = await (requestMessage.Content?.ReadAsStringAsync(cancellationToken) ?? Task.FromResult<string>(null)).ConfigureAwait(false);
			if (!string.IsNullOrEmpty(bodyString))
			{
				//may contain the password for some things, User models, censor it
				CensorBodyString<UserUpdate>(x => x.Password, ref bodyString);
				CensorBodyString<Repository>(x => x.AccessToken, ref bodyString);
				CensorBodyString<ChatBot>(x => x.ConnectionString, ref bodyString);

				bodyPart = string.Format(CultureInfo.InvariantCulture, " => {0}", bodyString);
			}

			AddToConsole($"{requestMessage.Method} {requestMessage.RequestUri}{instancePart}{bodyPart}");
		}

		public async Task LogResponse(HttpResponseMessage responseMessage, CancellationToken cancellationToken)
		{
			var requestMessage = responseMessage.RequestMessage;
			var instancePart = string.Empty;
			if (requestMessage.Headers.TryGetValues("Instance", out var values))
				instancePart = string.Format(CultureInfo.InvariantCulture, " I:{0}", values.First());

			if (responseMessage.Content.Headers.ContentType?.MediaType == MediaTypeNames.Application.Octet)
			{
				AddToConsole($"HTTP {responseMessage.StatusCode}: {requestMessage.Method} {requestMessage.RequestUri}{instancePart} => <STREAM>");
				return;
			}

			var bodyString = await (responseMessage.Content?.ReadAsStringAsync(cancellationToken) ?? Task.FromResult<string>(null)).ConfigureAwait(false);
			if (responseMessage.StatusCode == HttpStatusCode.InternalServerError)
			{
				var tmpFile = Path.Combine(Path.GetTempPath(), "tgs_error.html");
				File.WriteAllText(tmpFile, bodyString);
				ControlPanel.LaunchUrl(string.Format("file:///{0}", tmpFile));
				bodyString = string.Format("Error page written to {0}", tmpFile);
			}
			var bodyPart = string.Empty;
			if (!string.IsNullOrEmpty(bodyString))
			{
				CensorBodyString<Repository>(x => x.AccessToken, ref bodyString);
				CensorBodyString<ChatBot>(x => x.ConnectionString, ref bodyString);
				CensorBodyString<Token>(x => x.Bearer, ref bodyString);

				bodyPart = string.Format(CultureInfo.InvariantCulture, " => {0}", bodyString);
			}

			AddToConsole($"HTTP {responseMessage.StatusCode}: {requestMessage.Method} {requestMessage.RequestUri}{instancePart}{bodyPart}");
		}

		public void AddToConsole(string message)
		{
			lock (this)
				ConsoleContent = $"{ConsoleContent.Trim()}{Environment.NewLine}[{DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}]: {message}{Environment.NewLine}";
		}

		public bool CanRunCommand(MainWindowCommand command)
		{
            return command switch
            {
                MainWindowCommand.NewServerConnection or MainWindowCommand.CopyConsole or MainWindowCommand.ReportIssue => true,
                MainWindowCommand.AppUpdate => updater.Functional && updateReady,
                _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!"),
            };
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
							Password = string.Empty
						},
						Username = string.Empty,
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
					var clipboard = string.Join(" ", tmp);
					TextCopy.Clipboard.SetText(clipboard);
					break;
				case MainWindowCommand.AppUpdate:
					if (!updateInstalled)
					{
						UpdateProgress = 0;
						UpdateText = "Installing Update: ";
						await updater.ApplyUpdate(progress => UpdateProgress = progress);
						updateInstalled = updater.CanRestart;
						updateReady = true;
						UpdateText = "Update Ready!";
						AppUpdate.Recheck();
					}
					else
						updater.RestartApp();
					break;
				case MainWindowCommand.ReportIssue:
					var body = string.Format("<Please describe your issue here>{0}{0}{0}Console:{0}```{0}{1}{0}```{0}Reported from version {2}", Environment.NewLine, ConsoleContent, Assembly.GetExecutingAssembly().GetName().Version);
					ControlPanel.LaunchUrl(string.Format(CultureInfo.InvariantCulture, "https://github.com/tgstation/Tgstation.Server.ControlPanel/issues/new?body={0}", HttpUtility.UrlEncode(body)));
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public async void AsyncStart()
		{
			var updatesTask = CheckForUpdates();
			Connections = new List<ConnectionManagerViewModel>(settings.Connections.Select(x => CreateConnection(x)));
			await updatesTask.ConfigureAwait(true);
			if (Connections.Count == 1)
				PageContext.ActiveObject = Connections.First();
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

			if (newVersion != null && newVersion > Assembly.GetExecutingAssembly().GetName().Version)
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
