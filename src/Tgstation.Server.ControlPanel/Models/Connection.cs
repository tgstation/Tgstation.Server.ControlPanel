using System;
using Tgstation.Server.Api.Models;

namespace Tgstation.Server.ControlPanel.Models
{
	public sealed class Connection
	{
		public Uri Url { get; set; }

		public TimeSpan Timeout { get; set; }

		public Credentials Credentials { get; set; }

		public Token LastToken { get; set; }
	}
}
