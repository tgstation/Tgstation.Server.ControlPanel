using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class InstanceUserViewModel : ITreeNode, IInstanceUserRightsProvider
	{
		public string Title => rightsProvider == this ? "User Permissions" : String.Format(CultureInfo.InvariantCulture, "TODO: Someone Else ({0})", instanceUser.UserId);

		public string Icon => "resm:Tgstation.Server.ControlPanel.Assets.user.png";

		public bool IsExpanded { get; set; }

		public IReadOnlyList<ITreeNode> Children => null;

		public InstanceUserRights InstanceUserRights => instanceUser.InstanceUserRights.Value;

		public RepositoryRights RepositoryRights => instanceUser.RepositoryRights.Value;

		public ByondRights ByondRights => instanceUser.ByondRights.Value;

		public DreamMakerRights DreamMakerRights => instanceUser.DreamMakerRights.Value;

		public DreamDaemonRights DreamDaemonRights => instanceUser.DreamDaemonRights.Value;

		public ChatBotRights ChatBotRights => instanceUser.ChatBotRights.Value;

		public ConfigurationRights ConfigurationRights => instanceUser.ConfigurationRights.Value;

		public AdministrationRights AdministrationRights => userRightsProvider.AdministrationRights;

		public InstanceManagerRights InstanceManagerRights => userRightsProvider.InstanceManagerRights;

		readonly PageContextViewModel pageContext;
		readonly IUserRightsProvider userRightsProvider;
		readonly IInstanceUserClient users;
		readonly IInstanceUserRightsProvider rightsProvider;
		InstanceUser instanceUser;

		public event EventHandler OnUpdated;

		public InstanceUserViewModel(PageContextViewModel pageContext, IUserRightsProvider userRightsProvider, IInstanceUserClient users, InstanceUser instanceUser, IInstanceUserRightsProvider rightsProvider)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.userRightsProvider = userRightsProvider ?? throw new ArgumentNullException(nameof(userRightsProvider));
			this.users = users ?? throw new ArgumentNullException(nameof(users));
			this.instanceUser = instanceUser ?? throw new ArgumentNullException(nameof(instanceUser));
			this.rightsProvider = rightsProvider ?? this;

			userRightsProvider.OnUpdated += (a, b) => OnUpdated?.Invoke(this, new EventArgs());
		}

		public Task HandleDoubleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}
	}
}