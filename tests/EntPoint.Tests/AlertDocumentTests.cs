using EntPoint.Core;
using EntPoint.Persistence;
using Xunit;

namespace EntPoint.Tests
{
	public sealed class AlertDocumentTests
	{
		[Fact]
		public void FromEvent_MapsCompleteAlert()
		{
			NormalizedSecurityEvent securityEvent = new NormalizedSecurityEvent(
				"2026-09-02T10:00:00.0000000+00:00",
				Guid.Parse("ea673774-fd68-468d-ad54-05a540bd5292"),
				EndpointOperatingSystem.Windows,
				SecurityEventTypes.ProcessStart,
				@"CORP\alex",
				"powershell.exe",
				4200,
				4000,
				true,
				AlertScore: 91,
				AlertReason: "Suspicious process activity");

			AlertDocument document = AlertDocument.FromEvent(securityEvent);

			Assert.Equal(securityEvent.EndpointId, document.EndpointId);
			Assert.Equal("windows", document.OperatingSystem);
			Assert.Equal(91, document.AlertScore);
			Assert.True(document.IsAlert);
		}

		[Fact]
		public void FromEvent_RejectsNonAlert()
		{
			NormalizedSecurityEvent securityEvent = new NormalizedSecurityEvent(
				"2026-09-02T10:00:00.0000000+00:00",
				Guid.NewGuid(),
				EndpointOperatingSystem.Linux,
				SecurityEventTypes.ProcessStart,
				"alex",
				"bash",
				1200,
				1000,
				false);

			Assert.Throws<ArgumentException>(() => AlertDocument.FromEvent(securityEvent));
		}
	}
}
