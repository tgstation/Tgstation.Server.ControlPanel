using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.ControlPanel.Models;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	public sealed class ConnectionManagerViewModel : ViewModelBase, ICommandReceiver<ConnectionManagerViewModel.ConnectionManagerCommand>
	{
		const string HttpPrefix = "http://";
		const string HttpsPrefix = "https://";

		public enum ConnectionManagerCommand
		{
			Connect,
			Delete
		}

		public string ServerAddress {
			get => connection.Url.ToString();
			set
			{
				if (!value.StartsWith(usingHttp ? HttpPrefix : HttpsPrefix, StringComparison.OrdinalIgnoreCase))
					return;
				connection.Url = new Uri(value);
			}
		}

		public string ConnectionWord => "Connect";

		public string DeleteWord => confirmingDelete ? "Delete" : "Confirm?";

		public bool UsingHttp {
			get => usingHttp;
			set
			{
				if (usingHttp == value)
					return;
				usingHttp = value;

				connection.Url = new Uri(String.Concat(usingHttp ? HttpPrefix : HttpsPrefix, connection.Url.ToString().Remove(0, usingHttp ? HttpsPrefix.Length : HttpsPrefix.Length)));
				this.RaisePropertyChanged(nameof(ServerAddress));
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

		public string Password
		{
			get => connection.Credentials.Password;
			set => connection.Credentials.Password = value;
		}

		public string Username
		{
			get => connection.Credentials.Username;
			set => connection.Credentials.Username = value;
		}

		public bool AllowSavingPassword
		{
			get => connection.Credentials.AllowSavingPassword;
			set => connection.Credentials.AllowSavingPassword = value;
		}

		readonly Connection connection;

		readonly Action onDelete;
		
		bool usingHttp;
		bool confirmingDelete;

		public ConnectionManagerViewModel(Connection connection, Action onDelete)
		{
			this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
			this.onDelete = onDelete ?? throw new ArgumentNullException(nameof(onDelete));
		}

		public bool CanRunCommand(ConnectionManagerCommand command)
		{
			switch (command)
			{
				case ConnectionManagerCommand.Delete:
					return true;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public Task RunCommand(ConnectionManagerCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case ConnectionManagerCommand.Delete:
					if (confirmingDelete)
						onDelete();
					else
					{
						async void DeleteConfirmTimeout()
						{
							await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
							confirmingDelete = false;
							this.RaisePropertyChanged(nameof(DeleteWord));
						}
						confirmingDelete = true;
						this.RaisePropertyChanged(nameof(DeleteWord));
						DeleteConfirmTimeout();
					}
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
			return Task.CompletedTask;
		}
	}
}
