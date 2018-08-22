using System.Collections.Generic;

namespace Tgstation.Server.ControlPanel.Models
{
	public sealed class UserSettings
	{
#pragma warning disable CA2227 // Collection properties should be read only
		public List<Connection> Connections { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
	}
}
