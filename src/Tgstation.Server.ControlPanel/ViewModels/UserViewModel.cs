using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Client;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	internal class UserViewModel : ITreeNode
	{
		public string Title => user.Name;

		public string Icon => throw new System.NotImplementedException();

		public IReadOnlyList<ITreeNode> Children => null;

		readonly IServerClient serverClient;

		User user;

		public UserViewModel(IServerClient serverClient, User user)
		{
			this.serverClient = serverClient ?? throw new ArgumentNullException(nameof(serverClient));
			this.user = user ?? throw new ArgumentNullException(nameof(user));
		}

		public Task HandleDoubleClick(CancellationToken cancellationToken)
		{
			throw new System.NotImplementedException();
		}
	}
}