using System.Collections.Generic;

namespace Tgstation.Server.ControlPanel.Models
{
	public sealed class UserSettings
	{
		public List<Connection> Connections { get; set; }

		public Credentials GitHubToken { get; set; }
	}
}
