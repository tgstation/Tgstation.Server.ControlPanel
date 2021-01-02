using System;
using Tgstation.Server.Api.Rights;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	interface IInstanceUserRightsProvider : IUserRightsProvider
	{
		InstancePermissionSetRights InstanceUserRights { get; }

		RepositoryRights RepositoryRights { get; }

		ByondRights ByondRights { get; }
		DreamMakerRights DreamMakerRights { get; }
		DreamDaemonRights DreamDaemonRights { get; }
		ChatBotRights ChatBotRights { get; }
		ConfigurationRights ConfigurationRights { get; }
		
	}
}