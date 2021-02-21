using System.Collections.Generic;
using Tgstation.Server.Api.Models.Response;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	interface IGroupsProvider
	{
		IReadOnlyList<UserGroupResponse> GetGroups();
	}
}
