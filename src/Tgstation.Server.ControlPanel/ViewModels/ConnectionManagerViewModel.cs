using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using ReactiveUI;
using Tgstation.Server.Api;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Models.Response;
using Tgstation.Server.Client;
using Tgstation.Server.ControlPanel.Models;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	public sealed class ConnectionManagerViewModel : ViewModelBase, ICommandReceiver<ConnectionManagerViewModel.ConnectionManagerCommand>, ITreeNode, IAsyncDisposable
	{
		public enum ConnectionManagerCommand
		{
			Connect,
			Delete,
			Close,
			Discord,
			GitHub,
			Keycloak,
			TGForums,
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
							Username = DefaultCredentials.AdminUserName;
							Password = DefaultCredentials.DefaultAdminUserPassword;
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
		public EnumCommand<ConnectionManagerCommand> GitHub { get; }
		public EnumCommand<ConnectionManagerCommand> Discord { get; }
		public EnumCommand<ConnectionManagerCommand> Keycloak { get; }
		public EnumCommand<ConnectionManagerCommand> TGForums { get; }

		readonly Connection connection;

		readonly IServerClientFactory serverClientFactory;
		readonly IRequestLogger requestLogger;

		readonly PageContextViewModel pageContext;

		readonly Action onDelete;

		readonly IServerJobSink jobSink;

		readonly Func<Octokit.IGitHubClient> gitHubClientFactory;

		IReadOnlyList<ITreeNode> children;

		IServerClient serverClient;

		DateTimeOffset? startedConnectingAt;

		UserViewModel userVM;

		public string GitHubToken
		{
			get => getGitHubToken();
			set => setGitHubToken(value);
		}

		bool usingHttp;
		bool confirmingDelete;
		bool usingDefaultCredentials;
		bool isExpanded;

		Action<string> setGitHubToken { get; }
		Func<string> getGitHubToken { get; }

		public ConnectionManagerViewModel(IServerClientFactory serverClientFactory, IRequestLogger requestLogger, Connection connection, PageContextViewModel pageContext, Action onDelete, IJobSink jobSink, Func<Octokit.IGitHubClient> gitHubClientFactory, Action<string> setGitHubToken, Func<string> getGitHubToken)
		{
			this.serverClientFactory = serverClientFactory ?? throw new ArgumentNullException(nameof(serverClientFactory));
			this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
			this.requestLogger = requestLogger ?? throw new ArgumentNullException(nameof(requestLogger));
			this.onDelete = onDelete ?? throw new ArgumentNullException(nameof(onDelete));
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.jobSink = jobSink?.GetServerSink(() => serverClient, () => connection.JobRequeryRate, () => Title, () => userVM?.User) ?? throw new ArgumentNullException(nameof(jobSink));
			this.gitHubClientFactory = gitHubClientFactory ?? throw new ArgumentNullException(nameof(gitHubClientFactory));
			this.setGitHubToken = setGitHubToken ?? throw new ArgumentNullException(nameof(setGitHubToken));
			this.getGitHubToken = getGitHubToken ?? throw new ArgumentNullException(nameof(getGitHubToken));

			Connect = new EnumCommand<ConnectionManagerCommand>(ConnectionManagerCommand.Connect, this);
			Close = new EnumCommand<ConnectionManagerCommand>(ConnectionManagerCommand.Close, this);
			Delete = new EnumCommand<ConnectionManagerCommand>(ConnectionManagerCommand.Delete, this);
			GitHub = new EnumCommand<ConnectionManagerCommand>(ConnectionManagerCommand.GitHub, this);
			Discord = new EnumCommand<ConnectionManagerCommand>(ConnectionManagerCommand.Discord, this);
			Keycloak = new EnumCommand<ConnectionManagerCommand>(ConnectionManagerCommand.Keycloak, this);
			TGForums = new EnumCommand<ConnectionManagerCommand>(ConnectionManagerCommand.TGForums, this);

			usingHttp = !connection.Url.ToString().StartsWith(HttpsPrefix, StringComparison.OrdinalIgnoreCase);
			if (connection.Credentials.Password != null && connection.Credentials.Password.Length > 0)
			{
				AllowSavingPassword = true;
				if (connection.Username == DefaultCredentials.AdminUserName && connection.Credentials.Password == DefaultCredentials.DefaultAdminUserPassword)
					UsingDefaultCredentials = true;
			}
		}

		public async ValueTask DisposeAsync()
		{
			Children = null;
			jobSink.Dispose();
			await Disconnect();
		}

		async ValueTask Disconnect()
		{
			if (serverClient != null)
				await serverClient.DisposeAsync();
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
				ServerInformationResponse serverInfo;
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
					await BeginConnect(null, cancellationToken).ConfigureAwait(true);
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
				UserResponse user;
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
					newChildren.Add(new InstanceRootViewModel(pageContext, serverInfo, serverClient.Instances, userVM, urVM, jobSink, gitHubClientFactory(), connection.Url.Host));
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
				return BeginConnect(null, cancellationToken);
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


		public async Task BeginConnect(OAuthProvider? oAuthProvider, CancellationToken cancellationToken)
		{
			if (Connecting)
				throw new InvalidOperationException("Already connecting!");
			await Disconnect();
			Children = null;

			if (!await HandleConnectException(async () =>
			 {
				 if (!oAuthProvider.HasValue)
					 serverClient = await serverClientFactory.CreateFromLogin(connection.Url, connection.Username, connection.Credentials.Password, new List<IRequestLogger> { requestLogger }, connection.Timeout, true, cancellationToken).ConfigureAwait(true);
				 else
					 serverClient = await AttemptOAuthConnection(oAuthProvider.Value, cancellationToken);
				 connection.LastToken = serverClient.Token;
			 }).ConfigureAwait(true))
				await PostConnect(connection, cancellationToken).ConfigureAwait(true);
		}

		async Task<IServerClient> AttemptOAuthConnection(OAuthProvider oAuthProvider, CancellationToken cancellationToken)
		{
			var serverInfo = await serverClientFactory.GetServerInformation(connection.Url, new List<IRequestLogger> { requestLogger }, connection.Timeout, cancellationToken);

			if(serverInfo?.OAuthProviderInfos.TryGetValue(oAuthProvider, out var providerInfo) != true)
				throw new NotSupportedException("This server does not support this OAuth provider!");

			Uri targetUrl = null;
			switch (oAuthProvider)
			{
				case OAuthProvider.Discord:
					targetUrl = new Uri($"https://discord.com/api/oauth2/authorize?response_type=code&client_id={HttpUtility.UrlEncode(providerInfo.ClientId)}&scope=identify&redirect_uri={HttpUtility.UrlEncode(providerInfo.RedirectUri.ToString())}");
					break;
				case OAuthProvider.GitHub:
					targetUrl = new Uri($"https://github.com/login/oauth/authorize?client_id={HttpUtility.UrlEncode(providerInfo.ClientId)}&redirect_uri={HttpUtility.UrlEncode(providerInfo.RedirectUri.ToString())}&allow_signup=false");
					break;
				case OAuthProvider.Keycloak:
					targetUrl = new Uri($"{providerInfo.ServerUrl}/protocol/openid-connect/auth?response_type=code&client_id={HttpUtility.UrlEncode(providerInfo.ClientId)}&scope=openid&redirect_uri={HttpUtility.UrlEncode(providerInfo.RedirectUri.ToString())}&allow_signup=false");
					break;
				case OAuthProvider.TGForums:
					targetUrl = new Uri($"https://tgstation13.org/phpBB/app.php/tgapi/oauth/auth?response_type=code&client_id={HttpUtility.UrlEncode(providerInfo.ClientId)}&redirect_uri={HttpUtility.UrlEncode(providerInfo.RedirectUri.ToString())}&scope=user");
					break;
			}

			await using var callbackServer = new OAuthCallbackServer(providerInfo.RedirectUri.Port);
			await callbackServer.Start(cancellationToken);
			using var browser = ControlPanel.LaunchUrl(targetUrl.ToString(), false);

			await Task.WhenAny(
				Task.Delay(TimeSpan.FromMinutes(10), cancellationToken),
				callbackServer.Response);

			if (!browser.HasExited)
				browser.Kill();

			var responseCode = await callbackServer.Response;

			return await serverClientFactory.CreateFromOAuth(connection.Url, responseCode, oAuthProvider, new List<IRequestLogger> { requestLogger }, connection.Timeout, cancellationToken);
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
				ConnectionManagerCommand.Discord or ConnectionManagerCommand.GitHub or ConnectionManagerCommand.Keycloak or ConnectionManagerCommand.TGForums => connection.Url?.Host != null && !Connecting,
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
						await DisposeAsync();
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
					await BeginConnect(null, cancellationToken).ConfigureAwait(false);
					break;
				case ConnectionManagerCommand.Discord:
					await BeginConnect(OAuthProvider.Discord, cancellationToken).ConfigureAwait(false);
					break;
				case ConnectionManagerCommand.GitHub:
					await BeginConnect(OAuthProvider.GitHub, cancellationToken).ConfigureAwait(false);
					break;
				case ConnectionManagerCommand.Keycloak:
					await BeginConnect(OAuthProvider.Keycloak, cancellationToken).ConfigureAwait(false);
					break;
				case ConnectionManagerCommand.TGForums:
					await BeginConnect(OAuthProvider.TGForums, cancellationToken).ConfigureAwait(false);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}
