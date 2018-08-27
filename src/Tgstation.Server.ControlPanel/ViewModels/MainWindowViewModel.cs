using Avalonia.Interactivity;
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
using Tgstation.Server.Client;
using Tgstation.Server.ControlPanel.Models;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	public class MainWindowViewModel : ViewModelBase, IDisposable, ICommandReceiver<MainWindowViewModel.MainWindowCommand>, IRequestLogger
	{
		public enum MainWindowCommand
		{
			NewServerConnection,
			CopyConsole
		}

		public static string Versions => String.Format(CultureInfo.InvariantCulture, "Version: {0}, API Version: {1}", Assembly.GetExecutingAssembly().GetName().Version, ApiHeaders.Version);

		public static string Meme
		{
			get
			{
				var memes = new List<string>
				{
					"Proudly created by Cyberboss",
					"True Canadian Beer",
					"Brainlet Resistant",
					"Need Milk",
					"Absolute Seperation",
					"Deleting Data Directory..."
				};
				return memes[new Random().Next(memes.Count)];
			}
		}

		public string ConsoleContent
		{
			get => consoleContent;
			set => this.RaiseAndSetIfChanged(ref consoleContent, value);
		}

		public ICommand AddServerCommand { get; }

		public ICommand CopyConsole { get; }

		public PageContextViewModel PageContext { get; }

		public List<ConnectionManagerViewModel> Connections
		{
			get => connections;
			private set => this.RaiseAndSetIfChanged(ref connections, value);
		}

		readonly IServerClientFactory serverClientFactory;
		readonly CancellationTokenSource settingsSaveLoopCts;
		readonly Task settingsSaveLoop;
		readonly UserSettings settings;

		readonly string storageDirectory;
		readonly string settingsPath;

		List<ConnectionManagerViewModel> connections;

		string consoleContent;

		public MainWindowViewModel()
		{
			var assemblyName = Assembly.GetExecutingAssembly().GetName();
			serverClientFactory = new ServerClientFactory(new ProductHeaderValue(assemblyName.Name, assemblyName.Version.ToString()));

			var storagePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			const string SettingsFileName = "settings.json";
			storageDirectory = Path.Combine(storagePath, assemblyName.Name);
			settingsPath = Path.Combine(storageDirectory, SettingsFileName);
			settings = LoadSettings().Result;

			settingsSaveLoopCts = new CancellationTokenSource();
			settingsSaveLoop = SettingsSaveLoop(settingsSaveLoopCts.Token);

			ConsoleContent = "Request details will be shown here...";

			PageContext = new PageContextViewModel();
			Connections = new List<ConnectionManagerViewModel>(settings.Connections.Select(x => CreateConnection(x)));

			AddServerCommand = new EnumCommand<MainWindowCommand>(MainWindowCommand.NewServerConnection, this);
			CopyConsole = new EnumCommand<MainWindowCommand>(MainWindowCommand.CopyConsole, this);
		}

		ConnectionManagerViewModel CreateConnection(Connection connection)
		{
			ConnectionManagerViewModel newManager = null;
			newManager = new ConnectionManagerViewModel(serverClientFactory, this, connection, PageContext, () =>
			{
				settings.Connections.Remove(connection);
				Connections = new List<ConnectionManagerViewModel>(Connections.Where(x => x != newManager));
			});
			return newManager;
		}

		async Task<UserSettings> LoadSettings()
		{
			UserSettings settings = null;
			try
			{
				var settingsFile = await File.ReadAllTextAsync(settingsPath).ConfigureAwait(false);
				settings = JsonConvert.DeserializeObject<UserSettings>(settingsFile);
			}
			catch (IOException) { }
			catch (JsonException) { }

			if (settings == null)
				settings = new UserSettings();
			settings.Connections = settings.Connections ?? new List<Connection>();
			return settings;
		}

		async Task SaveSettings()
		{
			try
			{
				Directory.CreateDirectory(storageDirectory);
				var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
				await File.WriteAllTextAsync(settingsPath, json).ConfigureAwait(false);
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
					await SaveSettings().ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException) { }
		}

		public void Dispose()
		{
			settingsSaveLoopCts.Cancel();
			settingsSaveLoop.GetAwaiter().GetResult();
			SaveSettings().GetAwaiter().GetResult();
		}

		public async Task LogRequest(HttpRequestMessage requestMessage, CancellationToken cancellationToken)
		{
			var instancePart = String.Empty;
			if (requestMessage.Headers.TryGetValues("Instance", out var values))
				instancePart = String.Format(CultureInfo.InvariantCulture, " I:{0}", values.First());
			var bodyPart = String.Empty;
			var bodyString = await (requestMessage.Content?.ReadAsStringAsync() ?? Task.FromResult<string>(null)).ConfigureAwait(false);
			if (!String.IsNullOrEmpty(bodyString))
				bodyPart = String.Format(CultureInfo.InvariantCulture, " => {0}", bodyString);
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
					return true;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public Task RunCommand(MainWindowCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case MainWindowCommand.NewServerConnection:
					var newConnection = new Connection
					{
						Credentials = new Credentials
						{
							Username = String.Empty,
							Password = String.Empty
						},
						Timeout = TimeSpan.FromSeconds(15),
						Url = new Uri("https://localhost:5000")
					};

					settings.Connections.Add(newConnection);
					using (DelayChangeNotifications())
					{
						var newCm = CreateConnection(newConnection);
						PageContext.ActiveObject = newCm;
						var newConnections = new List<ConnectionManagerViewModel>(Connections);
						newConnections.Add(newCm);
						Connections = newConnections;
					}
					break;
				case MainWindowCommand.CopyConsole:
					var tmp = new List<string>(ConsoleContent.Split('\n'));
					tmp.RemoveAt(0);    //remove info line
					var clipboard = String.Join(' ', tmp);
					TextCopy.Clipboard.SetText(clipboard);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
			return Task.CompletedTask;
		}
		public void OnTreeNodeDoubleClick(object sender, RoutedEventArgs mouseEvtArgs)
		{
		}
	}
}
