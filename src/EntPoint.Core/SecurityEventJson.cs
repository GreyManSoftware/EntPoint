using System.Text.Json;
using System.Text.Json.Serialization;

namespace EntPoint.Core
{
	public static class SecurityEventJson
	{
		public static JsonSerializerOptions Options { get; } = new()
		{
			PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			Converters =
			{
				new JsonStringEnumConverter<EndpointOperatingSystem>(JsonNamingPolicy.SnakeCaseLower)
			}
		};
	}
}
