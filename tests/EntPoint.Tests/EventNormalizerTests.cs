using System.Text.Json;
using EntPoint.Core;
using Xunit;

namespace EntPoint.Tests
{
	public sealed class EventNormalizerTests
	{
		private static readonly Guid EndpointId = Guid.Parse("fca1572a-90cf-4899-b28c-4a8c73fcc674");
		private readonly EventNormalizer _normalizer = new();

		[Fact]
		public void Normalize_ConvertsTimestampToUtcAndDerivesNonAlert()
		{
			RawSecurityEvent rawEvent = CreateEvent(timestamp: "2026-09-02T10:00:00+01:00");

			NormalizationResult result = _normalizer.Normalize(rawEvent);

			Assert.True(result.IsAccepted);
			Assert.Equal("2026-09-02T09:00:00.0000000+00:00", result.Event!.Timestamp);
			Assert.False(result.Event.IsAlert);
			Assert.Null(result.Event.AlertScore);
		}

		[Fact]
		public void Normalize_DerivesAlertFromScore()
		{
			RawSecurityEvent rawEvent = CreateEvent(
				alertScore: 82,
				alertReason: "Suspicious process activity");

			NormalizationResult result = _normalizer.Normalize(rawEvent);

			Assert.True(result.IsAccepted);
			Assert.True(result.Event!.IsAlert);
			Assert.Equal(82, result.Event.AlertScore);
		}

		[Theory]
		[InlineData("system_idle_process")]
		[InlineData("SVCHOST.EXE")]
		[InlineData("kthreadd")]
		public void Normalize_FiltersDenylistedProcesses(string processName)
		{
			NormalizationResult result = _normalizer.Normalize(CreateEvent(processName: processName));

			Assert.False(result.IsAccepted);
			Assert.Equal("Process is on the denylist.", result.RejectionReason);
		}

		[Fact]
		public void Normalize_RejectsInvalidTimestamp()
		{
			NormalizationResult result = _normalizer.Normalize(
				CreateEvent(timestamp: "not-a-timestamp"));

			Assert.False(result.IsAccepted);
			Assert.Contains("Timestamp", result.RejectionReason);
		}

		[Fact]
		public void Normalize_RejectsEmptyEndpointId()
		{
			RawSecurityEvent rawEvent = CreateEvent() with { EndpointId = Guid.Empty };

			NormalizationResult result = _normalizer.Normalize(rawEvent);

			Assert.False(result.IsAccepted);
			Assert.Contains("Endpoint ID", result.RejectionReason);
		}

		[Fact]
		public void Normalize_RejectsAlertReasonWithoutScore()
		{
			RawSecurityEvent rawEvent = CreateEvent() with { AlertReason = string.Empty };

			NormalizationResult result = _normalizer.Normalize(rawEvent);

			Assert.False(result.IsAccepted);
			Assert.Contains("Alert score", result.RejectionReason);
		}

		[Fact]
		public void Serialize_UsesFlatSnakeCaseNdjsonShape()
		{
			NormalizedSecurityEvent normalized = _normalizer.Normalize(CreateEvent()).Event!;

			string json = JsonSerializer.Serialize(normalized, SecurityEventJson.Options);

			Assert.Contains("\"endpoint_id\"", json);
			Assert.Contains("\"operating_system\":\"windows\"", json);
			Assert.Contains("\"event_type\"", json);
			Assert.Contains("\"is_alert\":false", json);
			Assert.Contains("\"pid\":1200", json);
			Assert.DoesNotContain("\"alert_score\"", json);
		}

		private static RawSecurityEvent CreateEvent(
			string timestamp = "2026-09-02T09:00:00Z",
			string processName = "chrome.exe",
			int? alertScore = null,
			string? alertReason = null) =>
			new(
				timestamp,
				EndpointId,
				EndpointOperatingSystem.Windows,
				SecurityEventTypes.ProcessStart,
				"user-1001",
				processName,
				1200,
				1000,
				AlertScore: alertScore,
				AlertReason: alertReason);
	}
}
