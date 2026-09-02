using System.Text.Json;
using EntPoint.Core;

namespace EntPoint.Ingestion
{
	internal sealed class NdjsonEventReader
	{
		private readonly EventNormalizer _normalizer = new EventNormalizer();

		public async Task<IngestionBatch> ReadAsync(
			string inputPath,
			CancellationToken cancellationToken)
		{
			if (!File.Exists(inputPath))
			{
				throw new FileNotFoundException("NDJSON input file was not found.", inputPath);
			}

			List<NormalizedSecurityEvent> events = [];
			List<NormalizedSecurityEvent> alerts = [];
			await using FileStream stream = new FileStream(
				inputPath,
				FileMode.Open,
				FileAccess.Read,
				FileShare.ReadWrite,
				bufferSize: 4096,
				useAsync: true);
			using StreamReader reader = new StreamReader(stream);
			int lineNumber = 0;

			while (await reader.ReadLineAsync(cancellationToken) is string line)
			{
				lineNumber++;
				if (string.IsNullOrWhiteSpace(line))
				{
					continue;
				}

				NormalizedSecurityEvent securityEvent = ParseAndValidate(line, lineNumber);
				if (securityEvent.IsAlert)
				{
					alerts.Add(securityEvent);
				}
				else
				{
					events.Add(securityEvent);
				}
			}

			return new IngestionBatch(events, alerts);
		}

		private NormalizedSecurityEvent ParseAndValidate(string line, int lineNumber)
		{
			NormalizedSecurityEvent? securityEvent;
			try
			{
				securityEvent = JsonSerializer.Deserialize<NormalizedSecurityEvent>(
					line,
					SecurityEventJson.Options);
			}
			catch (JsonException exception)
			{
				throw new InvalidDataException(
					$"Line {lineNumber} is not valid event JSON.",
					exception);
			}

			if (securityEvent is null)
			{
				throw new InvalidDataException($"Line {lineNumber} contains no event.");
			}

			RawSecurityEvent rawEvent = new RawSecurityEvent(
				securityEvent.Timestamp,
				securityEvent.EndpointId,
				securityEvent.OperatingSystem,
				securityEvent.EventType,
				securityEvent.UserId,
				securityEvent.ProcessName,
				securityEvent.Pid,
				securityEvent.Ppid,
				securityEvent.FilePath,
				securityEvent.AlertScore,
				securityEvent.AlertReason);
			NormalizationResult result = _normalizer.Normalize(rawEvent);

			if (!result.IsAccepted || result.Event is null)
			{
				throw new InvalidDataException(
					$"Line {lineNumber} is invalid: {result.RejectionReason}");
			}

			if (result.Event.IsAlert != securityEvent.IsAlert)
			{
				throw new InvalidDataException(
					$"Line {lineNumber} has an incorrect is_alert value.");
			}

			return result.Event;
		}
	}
}
