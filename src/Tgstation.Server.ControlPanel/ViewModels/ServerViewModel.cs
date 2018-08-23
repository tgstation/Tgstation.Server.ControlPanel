using System;
using System.Collections.Generic;
using System.Globalization;
using Tgstation.Server.Client;
using Tgstation.Server.ControlPanel.Models;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	public class ServerViewModel : ViewModelBase, IDisposable, ITreeNode
	{
		const string LoadingGif = "resm:Tgstation.Server.ControlPanel.Assets.loading.gif";
		const string ErrorIcon = "resm:Tgstation.Server.ControlPanel.Assets.error.png";

		public string Title => connection.Url.ToString();

		public string Icon => "resm:Tgstation.Server.ControlPanel.Assets.tgs.ico";

		public IReadOnlyList<ITreeNode> Children { get; private set; }

		public int TimeoutSeconds
		{
			get => (int)Math.Ceiling(connection.Timeout.TotalSeconds);
			set => connection.Timeout = TimeSpan.FromSeconds(value);
		}

		public Credentials Credentials => connection.Credentials;

		public bool Connected => serverClient != null && serverClient.Token.ExpiresAt < DateTimeOffset.Now;

		public bool Connecting { get; private set; }

		public bool InvalidCredentials { get; private set; }
		public bool AccountLocked { get; private set; }

		readonly Connection connection;
		readonly IServerClientFactory serverClientFactory;
		readonly IRequestLogger requestLogger;

		sealed class BasicNode : ViewModelBase, ITreeNode
		{
			public string Title { get; set; }

			public string Icon { get; set; }

			public IReadOnlyList<ITreeNode> Children => null;
		}

		IServerClient serverClient;


		public ServerViewModel(IServerClientFactory serverClientFactory, Connection connection, IRequestLogger requestLogger)
		{
			this.serverClientFactory = serverClientFactory ?? throw new ArgumentNullException(nameof(serverClientFactory));
			this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
			this.requestLogger = requestLogger ?? throw new ArgumentNullException(nameof(requestLogger));

			if (connection.LastToken?.ExpiresAt.HasValue == true && connection.LastToken.ExpiresAt.Value < DateTimeOffset.Now)
			{
				serverClient = serverClientFactory.CreateServerClient(connection.Url, connection.LastToken, connection.Timeout);
				serverClient.AddRequestLogger(requestLogger);
				PostConnect();
			}
		}

		void PostConnect()
		{
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

			async void GetServerVersion()
			{
				try
				{
					var serverInfo = await serverClient.Version(default).ConfigureAwait(false);
					versionNode.Title = String.Format(CultureInfo.InvariantCulture, "Version: {0}", serverInfo.Version);
					apiVersionNode.Title = String.Format(CultureInfo.InvariantCulture, "API Version: {0}", serverInfo.Version);
					apiVersionNode.Icon = null;
					versionNode.Icon = null;
				}
				catch
				{
					versionNode.Icon = ErrorIcon;
					apiVersionNode.Icon = ErrorIcon;
				}
			}

			List<ITreeNode> childNodes = new List<ITreeNode>
			{
				versionNode,
				apiVersionNode
			};
			Children = childNodes;
			GetServerVersion();
		}

		async void BeginConnect()
		{
			if (Connecting)
				throw new InvalidOperationException("Already connecting!");

			Connecting = true;
			InvalidCredentials = false;
			AccountLocked = false;
			try
			{
				serverClient = await serverClientFactory.CreateServerClient(connection.Url, connection.Credentials.Username, connection.Credentials.Password, connection.Timeout).ConfigureAwait(false);
				serverClient.AddRequestLogger(requestLogger);
				PostConnect();
			}
			catch (UnauthorizedException)
			{
				InvalidCredentials = true;
			}
			catch (InsufficientPermissionsException)
			{
				AccountLocked = true;
			}
			finally
			{
				Connecting = false;
			}
			connection.LastToken = serverClient.Token;
		}

		public void Dispose()
		{
			Children = null;
			serverClient?.Dispose();
			serverClient = null;
		}
	}
}