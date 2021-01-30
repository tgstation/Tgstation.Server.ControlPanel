using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Client;
using Tgstation.Server.ControlPanel.Models;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	public sealed class ConnectionManagerViewModel : ViewModelBase, ICommandReceiver<ConnectionManagerViewModel.ConnectionManagerCommand>, ITreeNode, IDisposable
	{
		public enum ConnectionManagerCommand
		{
			Connect,
			Delete,
			Close
		}

		const string LoadingGif = "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png";
		const string ErrorIcon = "resm:Tgstation.Server.ControlPanel.Assets.error.png";
		const string InfoIcon = "resm:Tgstation.Server.ControlPanel.Assets.info.png";
		const string HttpPrefix = "http://";
		const string HttpsPrefix = "https://";

		public string Title => string.Format(CultureInfo.InvariantCulture, "{0} ({1})", connection.Url, userVM == null ? connection.Username : userVM.User.Name);
		public bool IsExpanded
		{
			get => isExpanded;
			set => this.RaiseAndSetIfChanged(ref isExpanded, value);
		}

		public string Icon
		{
			get
			{
				if (Connecting)
					return LoadingGif;
				if (AccountLocked)
					return "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg";
				if (ConnectionFailed)
					return ErrorIcon;
				if (InvalidCredentials)
					return "resm:Tgstation.Server.ControlPanel.Assets.unauth.png";
				if (ServerDown)
					return "resm:Tgstation.Server.ControlPanel.Assets.down.png";
				return "resm:Tgstation.Server.ControlPanel.Assets.tgs.ico";
			}
		}

		public IReadOnlyList<ITreeNode> Children
		{
			get => children;
			set => this.RaiseAndSetIfChanged(ref children, value);
		}

		public bool Connected => serverClient != null && serverClient.Token.ExpiresAt > DateTimeOffset.Now;

		public bool Connecting { get; private set; }
		public bool InvalidCredentials { get; private set; }
		public bool AccountLocked { get; private set; }
		public bool ConnectionFailed { get; private set; }
		public bool ServerDown { get; private set; }

		public string ServerAddress
		{
			get => connection.Url.ToString();
			set
			{
				if (!value.StartsWith(usingHttp ? HttpPrefix : HttpsPrefix, StringComparison.OrdinalIgnoreCase))
				{
					var oldUrl = connection.Url;
					connection.Url = null;
					Connect.Recheck();
					connection.Url = oldUrl;
					return;
				}
				try
				{
					connection.Url = new Uri(value);
					this.RaisePropertyChanged(nameof(ServerAddress));
					this.RaisePropertyChanged(nameof(Title));
					Connect.Recheck();
					jobSink.NameUpdate();
				}
				catch (UriFormatException) { }
			}
		}

		public string ConnectionWord
		{
			get
			{
				if (Connected)
					return "Refresh";
				if (Connecting)
				{
					var baseString = "Connecting";
					for (var I = 0; I < (DateTimeOffset.Now - startedConnectingAt.Value).TotalSeconds; ++I)
						baseString += '.';
					return baseString;
				}
				return "Connect";
			}
		}

		public string DeleteWord => !confirmingDelete ? "Delete" : "Confirm?";

		public bool UsingHttp
		{
			get => usingHttp;
			set
			{
				if (usingHttp == value)
					return;
				usingHttp = value;

				try
				{
					connection.Url = new Uri(string.Concat(usingHttp ? HttpPrefix : HttpsPrefix, connection.Url.ToString().Remove(0, usingHttp ? HttpsPrefix.Length : HttpPrefix.Length)));
					this.RaisePropertyChanged(nameof(ServerAddress));
					this.RaisePropertyChanged(nameof(Title));
					Connect.Recheck();
				}
				catch (UriFormatException) { }
			}
		}

		public double TimeoutMs
		{
			get => connection.Timeout.TotalMilliseconds;
			set
			{
				if (value < 1)
					return;
				connection.Timeout = TimeSpan.FromMilliseconds(Math.Floor(value));
			}
		}
		public double RequeryMs
		{
			get => connection.JobRequeryRate.TotalMilliseconds;
			set
			{
				if (value < 1)
					return;
				connection.JobRequeryRate = TimeSpan.FromMilliseconds(Math.Floor(value));
			}
		}

		public bool UsingDefaultCredentials
		{
			get => usingDefaultCredentials;
			set
			{
				using (DelayChangeNotifications())
				{
					var changed = usingDefaultCredentials != this.RaiseAndSetIfChanged(ref usingDefaultCredentials, value);
					if (changed)
					{
						if (value)
						{
							Username = User.AdminName;
							Password = User.DefaultAdminPassword;
						}
						else
							Password = string.Empty;
						Connect.Recheck();
					}
				}
			}
		}

		public string Password
		{
			get => connection.Credentials.Password;
			set
			{
				connection.Credentials.Password = value;
				this.RaisePropertyChanged();
				Connect.Recheck();
			}
		}

		public string Username
		{
			get => connection.Username;
			set
			{
				connection.Username = value;
				this.RaisePropertyChanged();
				Connect.Recheck();
			}
		}

		public bool AllowSavingPassword
		{
			get => connection.Credentials.AllowSavingPassword;
			set => connection.Credentials.AllowSavingPassword = value;
		}

		public string ErrorMessage { get; private set; }
		public bool Errored { get; private set; }

		public EnumCommand<ConnectionManagerCommand> Close { get; }
		public EnumCommand<ConnectionManagerCommand> Connect { get; }
		public EnumCommand<ConnectionManagerCommand> Delete { get; }

		readonly Connection connection;

		readonly IServerClientFactory serverClientFactory;
		readonly IRequestLogger requestLogger;

		readonly PageContextViewModel pageContext;

		readonly Action onDelete;

		readonly IServerJobSink jobSink;

		readonly Octokit.IGitHubClient gitHubClient;

		IReadOnlyList<ITreeNode> children;

		IServerClient serverClient;

		DateTimeOffset? startedConnectingAt;

		UserViewModel userVM;

		bool usingHttp;
		bool confirmingDelete;
		bool usingDefaultCredentials;
		bool isExpanded;

		public ConnectionManagerViewModel(IServerClientFactory serverClientFactory, IRequestLogger requestLogger, Connection connection, PageContextViewModel pageContext, Action onDelete, IJobSink jobSink, Octokit.IGitHubClient gitHubClient)
		{
			this.serverClientFactory = serverClientFactory ?? throw new ArgumentNullException(nameof(serverClientFactory));
			this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
			this.requestLogger = requestLogger ?? throw new ArgumentNullException(nameof(requestLogger));
			this.onDelete = onDelete ?? throw new ArgumentNullException(nameof(onDelete));
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.jobSink = jobSink?.GetServerSink(() => serverClient, () => connection.JobRequeryRate, () => Title, () => userVM?.User) ?? throw new ArgumentNullException(nameof(jobSink));
			this.gitHubClient = gitHubClient ?? throw new ArgumentNullException(nameof(gitHubClient));

			Connect = new EnumCommand<ConnectionManagerCommand>(ConnectionManagerCommand.Connect, this);
			Close = new EnumCommand<ConnectionManagerCommand>(ConnectionManagerCommand.Close, this);
			Delete = new EnumCommand<ConnectionManagerCommand>(ConnectionManagerCommand.Delete, this);

			usingHttp = !connection.Url.ToString().StartsWith(HttpsPrefix, StringComparison.OrdinalIgnoreCase);
			if (connection.Credentials.Password != null && connection.Credentials.Password.Length > 0)
			{
				AllowSavingPassword = true;
				if (connection.Username == User.AdminName && connection.Credentials.Password == User.DefaultAdminPassword)
					UsingDefaultCredentials = true;
			}
		}

		public void Dispose()
		{
			Children = null;
			jobSink.Dispose();
			Disconnect();
		}

		void Disconnect()
		{
			serverClient?.Dispose();
			userVM = null;
			this.RaisePropertyChanged(nameof(Title));
			serverClient = null;
			Children = null;
			pageContext.ActiveObject = this;
		}


		async Task PostConnect(Connection connection, CancellationToken cancellationToken)
		{
			IsExpanded = true;

			var versionNode = new BasicNode
			{
				Title = "Version",
				Icon = LoadingGif
			};

			var apiVersionNode = new BasicNode
			{
				Title = "API Version",
				Icon = LoadingGif
			};

			var dmapiVersionNode = new BasicNode
			{
				Title = "DMAPI Version",
				Icon = LoadingGif
			};

			var instanceLimitNode = new BasicNode
			{
				Title = "Instance Limit",
				Icon = LoadingGif
			};

			var userLimitNode = new BasicNode
			{
				Title = "User Limit",
				Icon = LoadingGif
			};

			var fakeUserNode = new BasicNode
			{
				Title = "Current User",
				Icon = LoadingGif
			};

			var fakeSwarmNode = new BasicNode
			{
				Title = "Swarm",
				Icon = LoadingGif
			};

			async Task GetServerVersionAndUserPerms()
			{
				ServerInformation serverInfo;
				var userInfoTask = serverClient.Users.Read(cancellationToken);
				try
				{
					serverInfo = await serverClient.ServerInformation(cancellationToken).ConfigureAwait(false);

					versionNode.Title = string.Format(CultureInfo.InvariantCulture, "{0}: {1}", versionNode.Title, serverInfo.Version);
					apiVersionNode.Title = string.Format(CultureInfo.InvariantCulture, "{0}: {1}", apiVersionNode.Title, serverInfo.ApiVersion);
					dmapiVersionNode.Title = string.Format(CultureInfo.InvariantCulture, "{0}: {1}", dmapiVersionNode.Title, serverInfo.DMApiVersion);
					instanceLimitNode.Title = string.Format(CultureInfo.InvariantCulture, "{0}: {1}", instanceLimitNode.Title, serverInfo.InstanceLimit);
					userLimitNode.Title = string.Format(CultureInfo.InvariantCulture, "{0}: {1}", userLimitNode.Title, serverInfo.UserLimit);
					fakeSwarmNode.Title = $"Swarm: {(serverInfo.SwarmServers == null ? "Disabled" : $"{serverInfo.SwarmServers.Count} Servers")}";
					versionNode.Icon = InfoIcon;
					apiVersionNode.Icon = InfoIcon;
					dmapiVersionNode.Icon = InfoIcon;
					instanceLimitNode.Icon = InfoIcon;
					userLimitNode.Icon = InfoIcon;
					fakeSwarmNode.Icon = InfoIcon;
				}
				catch (UnauthorizedException)
				{
					//bad token potentially
					await BeginConnect(cancellationToken).ConfigureAwait(true);
					return;
				}
				catch
				{
					versionNode.Icon = ErrorIcon;
					apiVersionNode.Icon = ErrorIcon;
					dmapiVersionNode.Icon = ErrorIcon;
					instanceLimitNode.Icon = ErrorIcon;
					userLimitNode.Icon = ErrorIcon;
					fakeSwarmNode.Icon = ErrorIcon;
					return;
				}
				versionNode.RaisePropertyChanged(nameof(Icon));
				fakeSwarmNode.RaisePropertyChanged(nameof(Icon));
				apiVersionNode.RaisePropertyChanged(nameof(Icon));
				dmapiVersionNode.RaisePropertyChanged(nameof(Icon));
				instanceLimitNode.RaisePropertyChanged(nameof(Icon));
				userLimitNode.RaisePropertyChanged(nameof(Icon));

				List<ITreeNode> newChildren;
				User user;
				try
				{
					user = await userInfoTask.ConfigureAwait(false);
					newChildren = new List<ITreeNode>(Children.Where(x => x != fakeUserNode));
					userVM = new UserViewModel(serverClient.Users, serverInfo, user, pageContext, null);
					this.RaisePropertyChanged(nameof(Title));
					newChildren.Add(userVM);
					newChildren.Add(new AdministrationViewModel(pageContext, serverClient.Administration, userVM, this, serverInfo.Version));
					var urVM = new UsersRootViewModel(serverClient.Users, serverClient.Groups, serverInfo, pageContext, userVM);
					newChildren.Add(urVM);
					newChildren.Add(new InstanceRootViewModel(pageContext, serverInfo, serverClient.Instances, userVM, urVM, jobSink, gitHubClient, connection.Url.Host));
				}
				catch
				{
					newChildren = new List<ITreeNode>(Children);
					fakeUserNode.Icon = ErrorIcon;
					fakeUserNode.RaisePropertyChanged(nameof(Icon));
				}

				Children = newChildren;
			}

			List<ITreeNode> childNodes = new List<ITreeNode>
			{
				versionNode,
				apiVersionNode,
				dmapiVersionNode,
				instanceLimitNode,
				userLimitNode,
				fakeSwarmNode,
				fakeUserNode,
			};
			Children = childNodes;
			await GetServerVersionAndUserPerms().ConfigureAwait(true);
		}

		public Task OnLoadConnect(CancellationToken cancellationToken)
		{
			if (connection.LastToken?.ExpiresAt != null && connection.LastToken.ExpiresAt > DateTimeOffset.Now)
				return PostConnect(connection, cancellationToken);
			else if (connection.Valid)
				return BeginConnect(cancellationToken);
			return Task.CompletedTask;
		}

		async Task<bool> HandleConnectException(Func<Task> action)
		{
			startedConnectingAt = DateTimeOffset.Now;
			Connecting = true;
			InvalidCredentials = false;
			AccountLocked = false;
			ServerDown = false;
			ConnectionFailed = false;
			ErrorMessage = null;
			Errored = false;

			this.RaisePropertyChanged(nameof(Icon));
			this.RaisePropertyChanged(nameof(ErrorMessage));
			this.RaisePropertyChanged(nameof(Errored));
			this.RaisePropertyChanged(nameof(ConnectionWord));
			this.RaisePropertyChanged(nameof(InvalidCredentials));
			this.RaisePropertyChanged(nameof(AccountLocked));
			this.RaisePropertyChanged(nameof(ConnectionFailed));
			this.RaisePropertyChanged(nameof(ServerDown));
			this.RaisePropertyChanged(nameof(Connected));
			Connect.Recheck();
			try
			{
				await action().ConfigureAwait(false);
				return false;
			}
			catch (UnauthorizedException)
			{
				InvalidCredentials = true;
				this.RaisePropertyChanged(nameof(InvalidCredentials));
				ErrorMessage = "Invalid credentials!";
			}
			catch (InsufficientPermissionsException)
			{
				AccountLocked = true;
				this.RaisePropertyChanged(nameof(AccountLocked));
				ErrorMessage = "Your account is disabled!";
			}
			catch (ServiceUnavailableException)
			{
				ServerDown = true;
				this.RaisePropertyChanged(nameof(ServerDown));
				ErrorMessage = "The server is not available!";
			}
			catch (ClientException e)
			{
				ConnectionFailed = true;
				this.RaisePropertyChanged(nameof(ConnectionFailed));

				ErrorMessage = string.Format(CultureInfo.InvariantCulture, "{0} (HTTP {1})", e.Message, e.ResponseMessage.StatusCode);
			}
			catch (HttpRequestException e)
			{
				ConnectionFailed = true;
				this.RaisePropertyChanged(nameof(ConnectionFailed));
				ErrorMessage = string.Format(CultureInfo.InvariantCulture, "An HTTP error occurred: {0}", (e.InnerException ?? e).Message);
			}
			finally
			{
				Connecting = false;
				Errored = ErrorMessage != null;
				this.RaisePropertyChanged(nameof(Icon));
				this.RaisePropertyChanged(nameof(ErrorMessage));
				this.RaisePropertyChanged(nameof(Errored));
				this.RaisePropertyChanged(nameof(Connected));
				this.RaisePropertyChanged(nameof(ConnectionWord));
				Connect.Recheck();
			}
			return true;
		}


		public async Task BeginConnect(CancellationToken cancellationToken)
		{
			if (Connecting)
				throw new InvalidOperationException("Already connecting!");
			Disconnect();
			Children = null;

			if (!await HandleConnectException(async () =>
			 {
				 serverClient = await serverClientFactory.CreateFromLogin(connection.Url, connection.Username, connection.Credentials.Password, new List<IRequestLogger> { requestLogger }, connection.Timeout, true, cancellationToken).ConfigureAwait(true);
				 connection.LastToken = serverClient.Token;
			 }).ConfigureAwait(true))
				await PostConnect(connection, cancellationToken).ConfigureAwait(true);
		}

		public Task HandleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(ConnectionManagerCommand command)
		{
			return command switch
			{
				ConnectionManagerCommand.Delete or ConnectionManagerCommand.Close => true,
				ConnectionManagerCommand.Connect => connection.Valid && !Connecting,
				_ => throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!"),
			};
		}

		public async Task RunCommand(ConnectionManagerCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case ConnectionManagerCommand.Delete:
					if (confirmingDelete)
					{
						pageContext.ActiveObject = null;
						onDelete();
						Dispose();
					}
					else
					{
						async void DeleteConfirmTimeout()
						{
							await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
							confirmingDelete = false;
							this.RaisePropertyChanged(nameof(DeleteWord));
						}
						confirmingDelete = true;
						this.RaisePropertyChanged(nameof(DeleteWord));
						DeleteConfirmTimeout();
					}
					break;
				case ConnectionManagerCommand.Close:
					pageContext.ActiveObject = null;
					break;
				case ConnectionManagerCommand.Connect:
					await BeginConnect(cancellationToken).ConfigureAwait(false);
					return;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}
