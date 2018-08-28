using System.Collections.Generic;
using Tgstation.Server.Api.Models;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	interface IUserProvider
	{
		User CurrentUser { get; }
		IReadOnlyList<User> GetUsers();
	}
}
