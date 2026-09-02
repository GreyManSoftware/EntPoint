using System.Text.Json;
using EntPoint.Core;
using EntPoint.Ingestion;
using Xunit;

namespace EntPoint.Tests
{
	public sealed class NdjsonEventReaderTests
	{
		[Fact]
		public async Task ReadAsync_SeparatesEventsAndAlerts()
		{
			string path = Path.GetTempFileName();
			try
			{
				NormalizedSecurityEvent normalEvent = CreateEvent(false);
				NormalizedSecurityEvent alertEvent = CreateEvent(true);
				string[] lines =
				[
					JsonSerializer.Serialize(normalEvent, SecurityEventJson.Options),
					JsonSerializer.Serialize(alertEvent, SecurityEventJson.Options)
				];
				await File.WriteAllLinesAsync(path, lines);

				NdjsonEventReader reader = new NdjsonEventReader();
				IngestionBatch batch = await reader.ReadAsync(path, CancellationToken.None);

				Assert.Single(batch.Events);
				Assert.Single(batch.Alerts);
				Assert.False(batch.Events[0].IsAlert);
				Assert.True(batch.Alerts[0].IsAlert);
			}
			finally
			{
				File.Delete(path);
			}
		}

		[Fact]
		public async Task ReadAsync_ReportsMalformedLineNumber()
		{
			string path = Path.GetTempFileName();
			try
			{
				await File.WriteAllLinesAsync(path, ["", "{invalid-json}"]);
				NdjsonEventReader reader = new NdjsonEventReader();

				InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
					() => reader.ReadAsync(path, CancellationToken.None));

				Assert.Contains("Line 2", exception.Message);
			}
			finally
			{
				File.Delete(path);
			}
		}

		private static NormalizedSecurityEvent CreateEvent(bool isAlert) =>
			new NormalizedSecurityEvent(
				"2026-09-02T10:00:00.0000000+00:00",
				Guid.Parse("d6a916dc-98a4-4bd6-893c-f14e31a84720"),
				EndpointOperatingSystem.Linux,
				SecurityEventTypes.FileRead,
				"alex",
				"bash",
				1200,
				1000,
				isAlert,
				"/etc/hosts",
				isAlert ? 80 : null,
				isAlert ? "Suspicious file read" : null);
	}
}
