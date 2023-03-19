using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Tgstation.Server.ControlPanel.Models
{
	[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Handled for linux deployments")]
	public class Credentials
	{
		[JsonIgnore]
		public bool AllowSavingPassword { get; set; }

		[JsonIgnore]
		public string Password
		{
			get => Decrypt();
			set => Encrypt(value);
		}

		public byte[] CipherText
		{
			get => AllowSavingPassword ? cipherText : null;
			set => cipherText = value;
		}

		public byte[] Entropy { get; set; }

		byte[] cipherText;


		void Encrypt(string cleartext)
		{
			if (cleartext == null)
			{
				CipherText = null;
				Entropy = null;
				return;
			}
			var clearTextBytes = Encoding.UTF8.GetBytes(cleartext);
			try
			{
				byte[] bentropy = new byte[20];
				using (var rng = RandomNumberGenerator.Create())
					rng.GetBytes(bentropy);

				CipherText = ProtectedData.Protect(clearTextBytes, bentropy, DataProtectionScope.CurrentUser);

				Entropy = bentropy;
			}
			catch (PlatformNotSupportedException)
			{
				CipherText = clearTextBytes;
			}
		}

		public string Decrypt()
		{
			if (cipherText == null)
				return null;
			byte[] clearTextBytes;
			try
			{
				clearTextBytes = ProtectedData.Unprotect(cipherText, Entropy, DataProtectionScope.CurrentUser);
			}
			catch (PlatformNotSupportedException)
			{
				clearTextBytes = cipherText;
			}
			return Encoding.UTF8.GetString(clearTextBytes);
		}

	}
}
