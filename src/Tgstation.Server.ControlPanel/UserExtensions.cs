using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Models.Response;

namespace Tgstation.Server.ControlPanel
{
	/// <summary>
	/// Extensions for the <see cref="User"/> <see langword="class"/>.
	/// </summary>
	static class UserExtensions
	{
		public static PermissionSet GetPermissionSet(this UserResponse user) => user.PermissionSet ?? user.Group.PermissionSet;
	}
}
