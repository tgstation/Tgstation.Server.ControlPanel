using Newtonsoft.Json;
using System;
using Tgstation.Server.Api.Models;

namespace Tgstation.Server.ControlPanel.Models
{
	public sealed class Connection
	{
		[JsonIgnore]
		public bool Valid => Url?.Host != null && !String.IsNullOrWhiteSpace(Credentials.Username) && Credentials.Password != null;


		public Uri Url { get; set; }

		public TimeSpan Timeout { get; set; }

		public Credentials Credentials { get; set; }

		public Token LastToken { get; set; }
	}
}
