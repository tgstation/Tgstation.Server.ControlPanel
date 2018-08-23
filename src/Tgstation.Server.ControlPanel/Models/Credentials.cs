using Newtonsoft.Json;
using System;
using System.Security.Cryptography;
using System.Text;

namespace Tgstation.Server.ControlPanel.Models
{
	public class Credentials
	{
		public string Username { get; set; }

		[JsonIgnore]
		public bool AllowSavingPassword { get; set; }

		[JsonIgnore]
		public string Password
		{
			get => Decrypt();
			set => Encrypt(value);
		}

#pragma warning disable CA1819 // Properties should not return arrays
		public byte[] CipherText
		{
			get => AllowSavingPassword ? cipherText : null;
			set => cipherText = value;
		}

		public byte[] Entropy { get; set; }
#pragma warning restore CA1819 // Properties should not return arrays

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
				using (var rng = new RNGCryptoServiceProvider())
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