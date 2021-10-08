using System;

using Tgstation.Server.Api.Models;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class OAuthSetting
	{
		public OAuthProvider Provider { get; }
		public string ProviderName => Provider.ToString() + " User ID:";
		public string ExternalUserId { get; set; }

		public OAuthSetting(string externalUserId, OAuthProvider oAuthProvider)
		{
			ExternalUserId = externalUserId ?? String.Empty;
			Provider = oAuthProvider;
		}
	}
}
