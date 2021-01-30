using Newtonsoft.Json;
using System;
using Tgstation.Server.Api.Models;

namespace Tgstation.Server.ControlPanel.Models
{
	public sealed class Connection
	{
		[JsonIgnore]
		public bool Valid => Url?.Host != null && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrEmpty(Credentials.Password);

		public string Username { get; set; }


		public Uri Url { get; set; }

		public TimeSpan Timeout { get; set; }
		public TimeSpan JobRequeryRate { get; set; }

		public Credentials Credentials { get; set; }

		public Token LastToken { get; set; }
	}
}
