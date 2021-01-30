using Tgstation.Server.Api.Models;

namespace Tgstation.Server.ControlPanel
{
	/// <summary>
	/// Extensions for the <see cref="User"/> <see langword="class"/>.
	/// </summary>
	static class UserExtensions
	{
		public static PermissionSet GetPermissionSet(this User user) => user.PermissionSet ?? user.Group.PermissionSet;
	}
}
