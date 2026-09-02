using System.Security.Cryptography;
using System.Text;

namespace EntPoint.Api.Security
{
	internal static class ApiKeyValidator
	{
		public static AuthenticatedApiKey? Validate(string? suppliedKey)
		{
			if (string.IsNullOrEmpty(suppliedKey))
			{
				return null;
			}

			if (FixedTimeEquals(suppliedKey, ApiKeyDefaults.AdminKey))
			{
				return new AuthenticatedApiKey("admin", ApiRoles.Admin);
			}

			if (FixedTimeEquals(suppliedKey, ApiKeyDefaults.AnalystKey))
			{
				return new AuthenticatedApiKey("analyst", ApiRoles.Analyst);
			}

			return null;
		}

		private static bool FixedTimeEquals(string suppliedKey, string expectedKey)
		{
			byte[] suppliedBytes = Encoding.UTF8.GetBytes(suppliedKey);
			byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
			return suppliedBytes.Length == expectedBytes.Length &&
				CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
		}
	}
}
