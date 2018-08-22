using System;
using System.Threading.Tasks;
using Tgstation.Server.Client;
using Tgstation.Server.ControlPanel.Models;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	public class ServerViewModel : IDisposable
	{
		public string Title => connection.Url.ToString();

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
		
		IServerClient serverClient;

		public ServerViewModel(IServerClientFactory serverClientFactory, Connection connection)
		{
			this.serverClientFactory = serverClientFactory ?? throw new ArgumentNullException(nameof(serverClientFactory));
			this.connection = connection ?? throw new ArgumentNullException(nameof(connection));

			if (connection.LastToken?.ExpiresAt.HasValue == true && connection.LastToken.ExpiresAt.Value < DateTimeOffset.Now)
				serverClient = serverClientFactory.CreateServerClient(connection.Url, connection.LastToken, connection.Timeout);
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
			serverClient?.Dispose();
			serverClient = null;
		}
	}
}