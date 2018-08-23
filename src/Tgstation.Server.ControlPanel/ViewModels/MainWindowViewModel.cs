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
			NewServerConnection
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

		public ConnectionManagerViewModel ConnectionManager
		{
			get => connectionManager;
			set => this.RaiseAndSetIfChanged(ref connectionManager, value);
		}

		public List<ServerViewModel> Connections { get; }

		readonly IServerClientFactory serverClientFactory;
		readonly CancellationTokenSource settingsSaveLoopCts;
		readonly Task settingsSaveLoop;
		readonly UserSettings settings;

		readonly string storageDirectory;
		readonly string settingsPath;

		string consoleContent;
		ConnectionManagerViewModel connectionManager;

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

			Connections = new List<ServerViewModel>(settings.Connections.Select(x => new ServerViewModel(serverClientFactory, x, this)));
			ConsoleContent = "Request details will be shown here...";

			AddServerCommand = new EnumCommand<MainWindowCommand>(MainWindowCommand.NewServerConnection, this);
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

#if DEBUG
			if (settings.Connections.Count == 0)
				//load default localhost admin
				settings.Connections.Add(new Connection
				{
					Credentials = new Credentials
					{
						Username = Api.Models.User.AdminName,
						Password = Api.Models.User.DefaultAdminPassword
					},
					Timeout = TimeSpan.FromSeconds(10),
					Url = new Uri("http://localhost:5000")
				});
#endif

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

		public Task LogRequest(HttpRequestMessage requestMessage, CancellationToken cancellationToken)
		{
			lock (this)
				ConsoleContent = String.Format(CultureInfo.InvariantCulture, "{0}{1}{2}", ConsoleContent, Environment.NewLine, requestMessage);
			return Task.CompletedTask;
		}

		public Task LogResponse(HttpResponseMessage responseMessage, CancellationToken cancellationToken)
		{
			lock (this)
				ConsoleContent = String.Format(CultureInfo.InvariantCulture, "{0}{1}{2}", ConsoleContent, Environment.NewLine, responseMessage);
			return Task.CompletedTask;
		}

		public bool CanRunCommand(MainWindowCommand command)
		{
			switch (command)
			{
				case MainWindowCommand.NewServerConnection:
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
						ConnectionManager = new ConnectionManagerViewModel(newConnection, () =>
						{
							settings.Connections.Remove(newConnection);
							ConnectionManager = null;
						});
						this.RaisePropertyChanged(nameof(Connections));
					}
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
