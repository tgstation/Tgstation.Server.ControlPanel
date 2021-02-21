using System.Collections.Generic;
using Tgstation.Server.Api.Models.Response;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	interface IUserProvider : IGroupsProvider
	{
		UserResponse CurrentUser { get; }
		IReadOnlyList<UserResponse> GetUsers();
		void ForceUpdate(UserResponse updatedUser);
	}
}
