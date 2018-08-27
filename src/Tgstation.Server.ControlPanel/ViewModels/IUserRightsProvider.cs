using System;
using Tgstation.Server.Api.Rights;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	interface IUserRightsProvider
	{
		event EventHandler OnUpdated;

		AdministrationRights AdministrationRights { get; }

		InstanceManagerRights InstanceManagerRights { get; }
	}
}