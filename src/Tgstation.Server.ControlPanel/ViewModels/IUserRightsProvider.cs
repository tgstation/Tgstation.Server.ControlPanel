using Tgstation.Server.Api.Rights;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	interface IUserRightsProvider
	{
		AdministrationRights AdministrationRights { get; }

		InstanceManagerRights InstanceManagerRights { get; }
	}
}